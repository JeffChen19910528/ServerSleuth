using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.Tests.Fakes;

/// <summary>GUI-5: a deterministic, in-memory <see cref="IGuiReportExportService"/> — never
/// touches a real file or the real Reporting APIs; records every call so a test can assert the
/// dashboard invoked it with exactly the arguments the user chose, and exactly once per click.
/// GUI-8C: parameter type changed from <c>ServerMigrationAssessmentReport</c> to
/// <see cref="ScanPipelineResult"/> to match the updated interface.</summary>
internal sealed class FakeGuiReportExportService : IGuiReportExportService
{
    public GuiReportExportResult ResultToReturn { get; set; } = GuiReportExportResult.Succeeded(["report.json", "report.html"]);

    public List<(ScanPipelineResult Pipeline, string OutputDirectory, ScanOutputFormat Format, ScanOverwritePolicy OverwritePolicy)> Calls { get; } = [];

    public GuiReportExportResult Export(
        ScanPipelineResult pipeline, string outputDirectory, ScanOutputFormat format, ScanOverwritePolicy overwritePolicy,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((pipeline, outputDirectory, format, overwritePolicy));
        return ResultToReturn;
    }
}
