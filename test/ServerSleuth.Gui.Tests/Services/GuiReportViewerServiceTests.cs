using System.IO;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.Tests.Services;

/// <summary>GUI-5 §2, §12: <see cref="GuiReportViewerService"/> — reads only a plain UTF-8 text
/// file inside <c>outputDirectory</c>, never regenerates anything, never triggers a scan. Uses a
/// real temp directory each test creates and deletes for itself (this class is explicitly NOT
/// part of the Reporting/Infrastructure boundary — see its own doc comment).</summary>
public sealed class GuiReportViewerServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "serversleuth-gui-viewer-tests-" + Guid.NewGuid());
    private readonly GuiReportViewerService _service = new();

    public GuiReportViewerServiceTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void ReadReportFile_ForAnExistingFile_ReturnsItsExactContent()
    {
        File.WriteAllText(Path.Combine(_directory, "report.json"), "{\"schemaVersion\":1}");

        var result = _service.ReadReportFile(_directory, "report.json");

        Assert.True(result.Success);
        Assert.Equal("{\"schemaVersion\":1}", result.Content);
    }

    [Fact]
    public void ReadReportFile_ForAMissingFile_FailsWithNotFound_NeverThrows()
    {
        var result = _service.ReadReportFile(_directory, "does-not-exist.json");

        Assert.False(result.Success);
        Assert.Equal(GuiReportViewFailureReason.NotFound, result.FailureReason);
        Assert.Null(result.Content);
    }

    [Theory]
    [InlineData("../secrets.txt")]
    [InlineData("..\\secrets.txt")]
    public void ReadReportFile_RejectsPathTraversal_NeverReadsOutsideTheOutputDirectory(string traversal)
    {
        var outsideFile = Path.Combine(Path.GetTempPath(), "serversleuth-gui-viewer-outside-" + Guid.NewGuid() + ".txt");
        File.WriteAllText(outsideFile, "not a report");
        try
        {
            var result = _service.ReadReportFile(_directory, traversal);

            Assert.False(result.Success);
            Assert.Equal(GuiReportViewFailureReason.NotFound, result.FailureReason);
        }
        finally
        {
            File.Delete(outsideFile);
        }
    }

    [Fact]
    public void ReadReportFile_RejectsARootedFileName()
    {
        var result = _service.ReadReportFile(_directory, Path.Combine(_directory, "report.json"));

        Assert.False(result.Success);
        Assert.Equal(GuiReportViewFailureReason.NotFound, result.FailureReason);
    }

    [Fact]
    public void ReadReportFile_NeverMutatesTheFileOnDisk()
    {
        var path = Path.Combine(_directory, "report.json");
        File.WriteAllText(path, "{\"a\":1}");
        var beforeBytes = File.ReadAllBytes(path);

        _service.ReadReportFile(_directory, "report.json");

        Assert.Equal(beforeBytes, File.ReadAllBytes(path));
    }
}
