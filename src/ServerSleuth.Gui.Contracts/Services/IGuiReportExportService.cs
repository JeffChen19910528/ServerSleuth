using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.Services;

/// <summary>
/// GUI-5 §1: the ONLY way <c>ServerSleuth.Gui</c> reaches the existing
/// <c>ServerSleuth.Reporting.Export.ReportArtifactFactory</c>/<c>IReportExporter</c> — the same
/// composition/execution boundary pattern <see cref="IGuiScanExecutor"/> already established in
/// GUI-3 (see <c>ServerSleuth.Gui.ExecutionHost</c>'s own project-file comment for the full
/// rationale: that project, not <c>ServerSleuth.Gui</c> itself, is the one place allowed to
/// reference <c>ServerSleuth.Reporting</c>). An implementation never re-analyzes, never
/// re-renders with a different renderer, and never mutates <paramref name="report"/> — it only
/// wraps the ALREADY-COMPLETE <see cref="ServerMigrationAssessmentReport"/> the Results Dashboard
/// already holds and writes it via the real exporter.
/// </summary>
public interface IGuiReportExportService
{
    GuiReportExportResult Export(
        ServerMigrationAssessmentReport report,
        string outputDirectory,
        ScanOutputFormat format,
        ScanOverwritePolicy overwritePolicy,
        CancellationToken cancellationToken = default);
}
