using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.Services;

/// <summary>
/// GUI-5 §2: reads an already-generated report file's raw text for the Results Dashboard's
/// "Open Report" action. Deliberately NOT part of the <c>Gui.ExecutionHost</c> boundary — reading
/// a plain UTF-8 text file the user's own completed scan already wrote to a directory the user
/// themselves chose is a local file read, not a Reporting/Infrastructure/Windows/Linux concern
/// (no scanner, no registry, no process, no network, no Reporting renderer is invoked here); it
/// never triggers a new scan and never regenerates the report.
/// </summary>
public interface IGuiReportViewerService
{
    GuiReportViewResult ReadReportFile(string outputDirectory, string fileName);
}
