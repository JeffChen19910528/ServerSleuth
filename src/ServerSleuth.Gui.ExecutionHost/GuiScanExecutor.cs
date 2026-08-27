using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Services;
using ServerSleuth.Infrastructure.Remote;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Gui.ExecutionHost;

/// <summary>
/// The real <see cref="IGuiScanExecutor"/> — the GUI-3 counterpart of
/// <c>ServerSleuth.Cli.Commands.ScanCommand</c>. Every stage below is a call into an existing,
/// unmodified engine (<see cref="ScanPipelineRunner"/>, moved to <c>ServerSleuth.Analysis</c> in
/// this same phase specifically so this class and the CLI share the literal same orchestration
/// code — see that type's own doc comment) or an existing Reporting API
/// (<see cref="ReportArtifactFactory"/>/<see cref="LocalFileReportExporter"/>) — this class
/// itself contains no discovery/correlation/risk/migration/reporting logic of its own.
///
/// The <paramref name="compositionFactory"/> constructor parameter is the same testability seam
/// <c>ServerSleuth.Cli.CliApplication</c> already established via its own
/// <c>Func&lt;ScanOptions, IServiceProvider&gt;</c> parameter — the public, parameterless
/// constructor (used by <c>CompositionRoot</c>) wires the real
/// <see cref="DefaultGuiScanComposition.Build"/>; tests supply a fake instead, without touching
/// a real Windows/Linux machine, SSH.NET, or WinRM (skill.md GUI-3 §13's explicit "use fake/
/// in-memory execution boundaries for most tests").
/// </summary>
public sealed class GuiScanExecutor : IGuiScanExecutor
{
    private readonly Func<ScanRequest, ScanCredentialInput, GuiScanComposition> _compositionFactory;
    private readonly ILogger<GuiScanExecutor> _logger;

    public GuiScanExecutor(ILogger<GuiScanExecutor>? logger = null)
        : this(DefaultGuiScanComposition.Build, logger)
    {
    }

    internal GuiScanExecutor(Func<ScanRequest, ScanCredentialInput, GuiScanComposition> compositionFactory, ILogger<GuiScanExecutor>? logger = null)
    {
        _compositionFactory = compositionFactory;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GuiScanExecutor>.Instance;
    }

    public async Task<ScanCompletionState> ExecuteAsync(
        ScanRequest request, ScanCredentialInput credentials, IProgress<ScanProgressState> progress, CancellationToken cancellationToken)
    {
        progress.Report(new ScanProgressState { Stage = ScanStage.Preparing });

        GuiScanComposition composition;
        try
        {
            composition = _compositionFactory(request, credentials);
        }
        catch (OperationCanceledException)
        {
            return ScanCompletionState.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare the scan target/transport.");
            return ScanCompletionState.Failed("Could not prepare the scan target. See application logs for details.");
        }

        using var provider = composition.Services as IDisposable;
        var transport = composition.Transport;

        // Phase 10D-2/10D-3B's own discipline, unchanged here: connect right before discovery
        // actually starts, never earlier — composition only ever CONSTRUCTS the transport.
        if (transport is SshRemoteTargetTransport sshTransport)
        {
            var connectResult = sshTransport.Connect(cancellationToken);
            if (!connectResult.Success)
            {
                return ScanCompletionState.Failed($"Could not connect to remote target '{transport.Target.Host}': {connectResult.ErrorMessage}");
            }
        }
        else if (transport is WindowsRemoteTargetTransport winRmTransport)
        {
            var connectResult = winRmTransport.Connect(cancellationToken);
            if (!connectResult.Success)
            {
                return ScanCompletionState.Failed($"Could not connect to remote target '{transport.Target.Host}': {connectResult.ErrorMessage}");
            }
        }

        try
        {
            var discoveryEngine = composition.Services.GetRequiredService<IDiscoveryEngine>();
            var pipelineRunner = new ScanPipelineRunner(discoveryEngine);

            progress.Report(new ScanProgressState { Stage = ScanStage.Discovery });
            var context = new DiscoveryContext { Profile = ScanProfile.Migration, CancellationToken = cancellationToken, Target = transport.Target };
            var discovery = await pipelineRunner.DiscoverAsync(context, cancellationToken);

            var scannerStatuses = discovery.ScannerResults
                .Select(r => new ScannerProgressInfo { ScannerId = r.ScannerId, Status = r.Status, EntityCount = r.Entities.Count })
                .ToList();
            progress.Report(new ScanProgressState { Stage = ScanStage.Discovery, ScannerStatuses = scannerStatuses, EntityCount = discovery.Entities.Count });

            cancellationToken.ThrowIfCancellationRequested();

            var pipelineResult = pipelineRunner.Analyze(discovery, cancellationToken, stage => progress.Report(new ScanProgressState
            {
                Stage = MapPipelineStage(stage),
                ScannerStatuses = scannerStatuses,
                EntityCount = discovery.Entities.Count
            }));

            progress.Report(new ScanProgressState { Stage = ScanStage.Export, ScannerStatuses = scannerStatuses, EntityCount = discovery.Entities.Count });
            var exportOutcome = ExportReport(pipelineResult.Report, request);

            if (!exportOutcome.Success)
            {
                foreach (var diagnostic in exportOutcome.Diagnostics)
                {
                    _logger.LogError("Report export diagnostic: {Diagnostic}", diagnostic);
                }

                return new ScanCompletionState
                {
                    Status = ScanExecutionStatus.Failed,
                    EntityCount = discovery.Entities.Count,
                    ErrorCount = discovery.Errors.Count,
                    ScannerStatuses = scannerStatuses,
                    ErrorMessage = "One or more report artifacts could not be written. See application logs for details."
                };
            }

            var hasPartialCoverage = discovery.ScannerStatuses.Values.Any(IsPartialStatus);

            return new ScanCompletionState
            {
                Status = hasPartialCoverage ? ScanExecutionStatus.Partial : ScanExecutionStatus.Completed,
                EntityCount = discovery.Entities.Count,
                ErrorCount = discovery.Errors.Count,
                ScannerStatuses = scannerStatuses,
                OutputPaths = exportOutcome.WrittenFileNames,
                // GUI-4 §Step2: the exact same pipelineResult instance already produced above —
                // never re-run, never re-derived — so the Results Dashboard can display it
                // without touching DiscoveryEngine/RiskRuleEngine/MigrationAssessmentEngine again.
                PipelineResult = pipelineResult
            };
        }
        catch (OperationCanceledException)
        {
            return ScanCompletionState.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure during scan execution.");
            return ScanCompletionState.Failed("An unexpected error occurred during the scan. See application logs for details.");
        }
    }

    private static ScanStage MapPipelineStage(PipelineStage stage) => stage switch
    {
        PipelineStage.Analysis => ScanStage.Analysis,
        PipelineStage.RiskAnalysis => ScanStage.RiskAnalysis,
        PipelineStage.MigrationAssessment => ScanStage.MigrationAssessment,
        PipelineStage.Reporting => ScanStage.Reporting,
        _ => ScanStage.Analysis
    };

    /// <summary>Mirrors <c>ServerSleuth.Core.Enums.ScannerStatus</c>'s own "partial coverage"
    /// classification exactly as <c>ServerSleuth.Cli.Output.ScanProgressReporter.IsPartialStatus</c>
    /// (internal to the CLI assembly, so not directly reusable here) already defines it — a
    /// one-line status-membership check, not pipeline logic, so restating it here is not the
    /// "duplicating the scan pipeline" skill.md GUI-3 warns against.</summary>
    private static bool IsPartialStatus(ScannerStatus status) =>
        status is ScannerStatus.PartiallySupported or ScannerStatus.AccessDenied or ScannerStatus.Failed;

    /// <summary>Writes exactly the requested format(s) via the existing
    /// <see cref="ReportArtifactFactory"/>/<see cref="LocalFileReportExporter"/> — the Reporting
    /// APIs themselves, never re-implemented. GUI-5 extracted the actual write logic into
    /// <see cref="GuiReportExportService.ExportReport"/> (shared with the Results Dashboard's own
    /// on-demand "Export Report" action) so this remains the ONLY call site that decides format/
    /// policy from a <see cref="ScanRequest"/> specifically — everything after that is one shared
    /// implementation, never two.</summary>
    private static GuiScanExportOutcome ExportReport(
        ServerSleuth.Analysis.Migration.Consolidation.ServerMigrationAssessmentReport report, ScanRequest request) =>
        GuiReportExportService.ExportReport(report, request.OutputDirectory, request.OutputFormat, request.OverwritePolicy);
}
