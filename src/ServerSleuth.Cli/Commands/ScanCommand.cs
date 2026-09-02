using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Options;
using ServerSleuth.Cli.Output;
using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Cli.Pipeline;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Infrastructure.Remote;
using ServerSleuth.Infrastructure.Targets;
#if SERVERSLEUTH_WINDOWS
using ServerSleuth.Windows.Remote;
#endif

namespace ServerSleuth.Cli.Commands;

/// <summary>
/// The <c>scan</c> command — orchestrates the existing pipeline end to end (skill.md Phase 10A
/// §7) and the existing Export layer (§10), printing concise progress along the way (§12). Every
/// stage is a call into an already-existing engine; this type contains no analysis/rendering/
/// export logic of its own.
/// </summary>
internal static class ScanCommand
{
    public static async Task<int> RunAsync(ScanOptions options, IServiceProvider services, IConsoleWriter console, CancellationToken cancellationToken)
    {
        var reporter = new ScanProgressReporter(console, options.Quiet, options.Verbose);
        reporter.WriteHeader();

        var discoveryEngine = services.GetRequiredService<IDiscoveryEngine>();
        var transport = services.GetRequiredService<ITargetTransport>();

        // Phase 10D-2 §6: connect here, right before discovery actually starts — never earlier
        // (composition only ever CONSTRUCTS the transport, never connects it).
        if (transport is SshRemoteTargetTransport sshTransport)
        {
            var connectResult = sshTransport.Connect(cancellationToken);
            if (!connectResult.Success)
            {
                console.WriteErrorLine($"Could not connect to remote target '{transport.Target.Host}': {connectResult.ErrorMessage}");
                return CliExitCode.GeneralFailure;
            }
        }
#if SERVERSLEUTH_WINDOWS
        else if (transport is WindowsRemoteTargetTransport winRmTransport)
        {
            var connectResult = winRmTransport.Connect(cancellationToken);
            if (!connectResult.Success)
            {
                console.WriteErrorLine($"Could not connect to remote target '{transport.Target.Host}': {connectResult.ErrorMessage}");
                return CliExitCode.GeneralFailure;
            }
        }
#endif

        var context = new DiscoveryContext { Profile = ScanProfile.Migration, CancellationToken = cancellationToken, Target = transport.Target };
        var pipelineRunner = new ScanPipelineRunner(discoveryEngine);

        reporter.WriteTarget(transport.Target);
        reporter.WriteDiscoveryStarting();
        var discoveryStopwatch = Stopwatch.StartNew();
        var discovery = await pipelineRunner.DiscoverAsync(context, cancellationToken);
        discoveryStopwatch.Stop();
        reporter.WriteDiscoveryComplete(discovery, discoveryStopwatch.Elapsed);

        reporter.WriteAnalyzing();
        var analysisStopwatch = Stopwatch.StartNew();
        var pipelineResult = pipelineRunner.Analyze(discovery, cancellationToken);
        analysisStopwatch.Stop();
        reporter.WriteAnalysisComplete(analysisStopwatch.Elapsed);
        reporter.WriteMigrationAssessmentComplete();
        reporter.WriteRiskSummary(pipelineResult.Aggregation.Server);
        reporter.WriteMigrationSummary(pipelineResult.Report.ServerSummary);

        reporter.WriteWritingReports();
        var exportOutcome = ReportExportRunner.Export(pipelineResult, options);
        foreach (var fileName in exportOutcome.WrittenFileNames)
        {
            reporter.WriteReportWritten(fileName);
        }

        if (!exportOutcome.Success)
        {
            foreach (var diagnostic in exportOutcome.Diagnostics)
            {
                console.WriteErrorLine(diagnostic);
            }

            return CliExitCode.ExportFailure;
        }

        reporter.WriteCompleted();

        var hasPartialCoverage = discovery.ScannerStatuses.Values.Any(ScanProgressReporter.IsPartialStatus);
        return hasPartialCoverage ? CliExitCode.PartialDiscovery : CliExitCode.Success;
    }
}
