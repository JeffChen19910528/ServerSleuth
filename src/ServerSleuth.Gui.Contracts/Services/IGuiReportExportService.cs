using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.Services;

/// <summary>
/// GUI-5 §1: the ONLY way <c>ServerSleuth.Gui</c> reaches the existing
/// <c>ServerSleuth.Reporting.Export.ReportArtifactFactory</c>/<c>IReportExporter</c> — the same
/// composition/execution boundary pattern <see cref="IGuiScanExecutor"/> already established in
/// GUI-3 (see <c>ServerSleuth.Gui.ExecutionHost</c>'s own project-file comment for the full
/// rationale: that project, not <c>ServerSleuth.Gui</c> itself, is the one place allowed to
/// reference <c>ServerSleuth.Reporting</c>). An implementation never re-analyzes, never
/// re-renders with a different renderer, and never mutates <paramref name="pipeline"/> — it only
/// wraps the ALREADY-COMPLETE <see cref="ScanPipelineResult"/> the Results Dashboard already
/// holds and writes it via the real exporter. GUI-8C: changed from
/// <c>ServerMigrationAssessmentReport</c> to <see cref="ScanPipelineResult"/> so inventory data
/// from <c>Discovery</c> / <c>Boundaries</c> / <c>ExternalDependencies</c> reaches the HTML
/// renderer and produces inventory-first reports.
/// </summary>
public interface IGuiReportExportService
{
    GuiReportExportResult Export(
        ScanPipelineResult pipeline,
        string outputDirectory,
        ScanOutputFormat format,
        ScanOverwritePolicy overwritePolicy,
        CancellationToken cancellationToken = default);
}
