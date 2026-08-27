using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.Tests.Fakes;

/// <summary>GUI-5: a deterministic, in-memory <see cref="IGuiReportViewerService"/> — never
/// touches a real file; records every call.</summary>
internal sealed class FakeGuiReportViewerService : IGuiReportViewerService
{
    public GuiReportViewResult ResultToReturn { get; set; } = GuiReportViewResult.Succeeded("{}");

    public List<(string OutputDirectory, string FileName)> Calls { get; } = [];

    public GuiReportViewResult ReadReportFile(string outputDirectory, string fileName)
    {
        Calls.Add((outputDirectory, fileName));
        return ResultToReturn;
    }
}
