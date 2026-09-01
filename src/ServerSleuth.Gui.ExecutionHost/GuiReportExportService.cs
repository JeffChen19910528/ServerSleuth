using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Services;
using ServerSleuth.Reporting.Export;

namespace ServerSleuth.Gui.ExecutionHost;

/// <summary>
/// The real <see cref="IGuiReportExportService"/> — invoked from the Results Dashboard's
/// "Export Report" action (GUI-5 §1) to re-export an already-completed scan's report on demand,
/// with a format/overwrite policy the user chooses at that moment (independent of whatever was
/// chosen during Scan Configuration). Delegates to the exact same
/// <see cref="ReportArtifactFactory"/>/<see cref="LocalFileReportExporter"/> calls
/// <c>GuiScanExecutor</c> already makes at the end of a scan — see
/// <see cref="ExportReport(ServerMigrationAssessmentReport, string, ScanOutputFormat, ScanOverwritePolicy)"/>,
/// the one shared implementation both call, so there is no second export code path.
/// </summary>
public sealed class GuiReportExportService : IGuiReportExportService
{
    public GuiReportExportResult Export(
        ScanPipelineResult pipeline,
        string outputDirectory,
        ScanOutputFormat format,
        ScanOverwritePolicy overwritePolicy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        if (string.IsNullOrWhiteSpace(outputDirectory) || outputDirectory.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return GuiReportExportResult.Failed(GuiReportExportFailureReason.InvalidPath, "The output directory is not a valid path.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var outcome = ExportReport(pipeline, outputDirectory, format, overwritePolicy);
        if (outcome.Success)
        {
            return GuiReportExportResult.Succeeded(outcome.WrittenFileNames);
        }

        var reason = outcome.Diagnostics.Any(d => d.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            ? GuiReportExportFailureReason.AlreadyExists
            : GuiReportExportFailureReason.WriteFailed;

        var message = reason == GuiReportExportFailureReason.AlreadyExists
            ? "One or more report files already exist at that location. Choose Overwrite or a different directory."
            : "One or more report artifacts could not be written. See application logs for details.";

        return GuiReportExportResult.Failed(reason, message);
    }

    /// <summary>The single shared implementation — mirrors <c>ServerSleuth.Cli.Pipeline.ReportExportRunner.Export</c>
    /// exactly, adapted only to the GUI's own <see cref="ScanOutputFormat"/>/<see cref="ScanOverwritePolicy"/>
    /// mirror enums. <c>GuiScanExecutor</c>'s end-of-scan export and this on-demand dashboard
    /// export both call this same method — never two independent export code paths.
    /// GUI-8C: accepts <see cref="ScanPipelineResult"/> so inventory data reaches the HTML renderer.</summary>
    internal static GuiScanExportOutcome ExportReport(
        ScanPipelineResult pipeline, string outputDirectory, ScanOutputFormat format, ScanOverwritePolicy overwritePolicy)
    {
        var bundle = ReportArtifactFactory.CreateBundle(pipeline);
        var exporter = new LocalFileReportExporter();
        var reportOverwritePolicy = overwritePolicy == ScanOverwritePolicy.Overwrite
            ? ReportOverwritePolicy.Overwrite
            : ReportOverwritePolicy.FailIfExists;

        var written = new List<string>();
        var diagnostics = new List<string>();
        var success = true;

        if (format is ScanOutputFormat.Json or ScanOutputFormat.Both)
        {
            var result = exporter.Export(bundle.Json, outputDirectory, reportOverwritePolicy);
            success &= result.Success;
            if (result.Success)
            {
                written.Add(bundle.Json.FileName);
            }
            else
            {
                diagnostics.AddRange(result.Diagnostics);
            }
        }

        if (format is ScanOutputFormat.Html or ScanOutputFormat.Both)
        {
            var result = exporter.Export(bundle.Html, outputDirectory, reportOverwritePolicy);
            success &= result.Success;
            if (result.Success)
            {
                written.Add(bundle.Html.FileName);
            }
            else
            {
                diagnostics.AddRange(result.Diagnostics);
            }
        }

        return new GuiScanExportOutcome { Success = success, WrittenFileNames = written, Diagnostics = diagnostics };
    }
}

internal sealed record GuiScanExportOutcome
{
    public required bool Success { get; init; }
    public required IReadOnlyList<string> WrittenFileNames { get; init; }
    public required IReadOnlyList<string> Diagnostics { get; init; }
}
