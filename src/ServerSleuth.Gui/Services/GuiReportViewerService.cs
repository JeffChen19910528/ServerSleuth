using System.IO;
using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.Services;

/// <summary>The only <see cref="IGuiReportViewerService"/> implementation — a plain, defensive
/// <see cref="File.ReadAllText(string)"/> over a file the exact same scan already wrote (its name
/// only ever comes from <c>ScanExecutionState.OutputPaths</c>, which the exporter itself already
/// validated as safe — see <see cref="ServerSleuth.Gui.ExecutionHost.GuiReportExportService"/>).
/// Re-checks the name here too (defense in depth, not trust) so this class never reads outside
/// <paramref name="outputDirectory"/> even if handed an unexpected value.</summary>
public sealed class GuiReportViewerService : IGuiReportViewerService
{
    public GuiReportViewResult ReadReportFile(string outputDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory) || string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(fileName)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return GuiReportViewResult.Failed(GuiReportViewFailureReason.NotFound, "The requested report file could not be located.");
        }

        var fullPath = Path.Combine(outputDirectory, fileName);

        try
        {
            if (!File.Exists(fullPath))
            {
                return GuiReportViewResult.Failed(GuiReportViewFailureReason.NotFound, "The requested report file could not be located.");
            }

            var content = File.ReadAllText(fullPath);
            return GuiReportViewResult.Succeeded(content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return GuiReportViewResult.Failed(GuiReportViewFailureReason.ReadFailed, "The report file could not be read. See application logs for details.");
        }
    }
}
