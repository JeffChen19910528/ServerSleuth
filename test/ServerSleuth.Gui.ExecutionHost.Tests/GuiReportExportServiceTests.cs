using System.IO;
using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Gui.ExecutionHost.Tests.Fixtures;
using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.ExecutionHost.Tests;

/// <summary>
/// GUI-5 §1, §12: <see cref="GuiReportExportService"/> exercised against the REAL
/// <c>ReportArtifactFactory</c>/<c>LocalFileReportExporter</c> (this is the one project allowed
/// to touch them) writing to a real temp directory each test creates and deletes for itself —
/// exactly the same isolation discipline <c>GuiScanExecutorTests</c> already established.
/// </summary>
public sealed class GuiReportExportServiceTests : IDisposable
{
    private readonly string _outputDirectory = Path.Combine(Path.GetTempPath(), "serversleuth-gui-export-tests-" + Guid.NewGuid());
    private readonly GuiReportExportService _service = new();

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void Export_Json_WritesOnlyReportJson()
    {
        var result = _service.Export(MinimalReportFixture.BuildPipeline(), _outputDirectory, ScanOutputFormat.Json, ScanOverwritePolicy.FailIfExists);

        Assert.True(result.Success);
        Assert.Equal(["report.json"], result.WrittenFileNames);
        Assert.True(File.Exists(Path.Combine(_outputDirectory, "report.json")));
        Assert.False(File.Exists(Path.Combine(_outputDirectory, "report.html")));
    }

    [Fact]
    public void Export_Html_WritesOnlyReportHtml()
    {
        var result = _service.Export(MinimalReportFixture.BuildPipeline(), _outputDirectory, ScanOutputFormat.Html, ScanOverwritePolicy.FailIfExists);

        Assert.True(result.Success);
        Assert.Equal(["report.html"], result.WrittenFileNames);
        Assert.True(File.Exists(Path.Combine(_outputDirectory, "report.html")));
        Assert.False(File.Exists(Path.Combine(_outputDirectory, "report.json")));
    }

    [Fact]
    public void Export_Both_WritesBothFiles_FromTheSameReportInstance()
    {
        var result = _service.Export(MinimalReportFixture.BuildPipeline(), _outputDirectory, ScanOutputFormat.Both, ScanOverwritePolicy.FailIfExists);

        Assert.True(result.Success);
        Assert.Equal(2, result.WrittenFileNames.Count);
        Assert.Contains("report.json", result.WrittenFileNames);
        Assert.Contains("report.html", result.WrittenFileNames);
    }

    [Fact]
    public void Export_FailIfExists_SecondExportToTheSameDirectory_Fails_AndNeverOverwrites()
    {
        var report = MinimalReportFixture.BuildPipeline();
        var first = _service.Export(report, _outputDirectory, ScanOutputFormat.Json, ScanOverwritePolicy.FailIfExists);
        Assert.True(first.Success);

        var beforeSecondAttempt = File.GetLastWriteTimeUtc(Path.Combine(_outputDirectory, "report.json"));
        var second = _service.Export(report, _outputDirectory, ScanOutputFormat.Json, ScanOverwritePolicy.FailIfExists);

        Assert.False(second.Success);
        Assert.Equal(GuiReportExportFailureReason.AlreadyExists, second.FailureReason);
        Assert.NotNull(second.ErrorMessage);
        Assert.DoesNotContain("Exception", second.ErrorMessage);
        Assert.Equal(beforeSecondAttempt, File.GetLastWriteTimeUtc(Path.Combine(_outputDirectory, "report.json")));
    }

    [Fact]
    public void Export_Overwrite_SecondExportToTheSameDirectory_Succeeds()
    {
        var report = MinimalReportFixture.BuildPipeline();
        var first = _service.Export(report, _outputDirectory, ScanOutputFormat.Json, ScanOverwritePolicy.Overwrite);
        Assert.True(first.Success);

        var second = _service.Export(report, _outputDirectory, ScanOutputFormat.Json, ScanOverwritePolicy.Overwrite);

        Assert.True(second.Success);
        Assert.Equal(["report.json"], second.WrittenFileNames);
    }

    [Fact]
    public void Export_ToAnInvalidPath_FailsWithInvalidPathReason_AndNeverThrows()
    {
        var invalidPath = "C:\\invalid" + new string(Path.GetInvalidPathChars());

        var result = _service.Export(MinimalReportFixture.BuildPipeline(), invalidPath, ScanOutputFormat.Json, ScanOverwritePolicy.FailIfExists);

        Assert.False(result.Success);
        Assert.Equal(GuiReportExportFailureReason.InvalidPath, result.FailureReason);
    }

    [Fact]
    public void Export_NeverMutatesTheReportItWasHandled()
    {
        var pipeline = MinimalReportFixture.BuildPipeline();
        var applicationCountBefore = pipeline.Report.ApplicationAssessments.Count;

        _service.Export(pipeline, _outputDirectory, ScanOutputFormat.Both, ScanOverwritePolicy.Overwrite);

        Assert.Equal(applicationCountBefore, pipeline.Report.ApplicationAssessments.Count);
    }

    [Fact]
    public void Export_CalledTwiceWithTheSameReportAndOverwrite_ProducesByteIdenticalOutput_Deterministic()
    {
        var report = MinimalReportFixture.BuildPipeline();
        _service.Export(report, _outputDirectory, ScanOutputFormat.Json, ScanOverwritePolicy.Overwrite);
        var firstBytes = File.ReadAllBytes(Path.Combine(_outputDirectory, "report.json"));

        _service.Export(report, _outputDirectory, ScanOutputFormat.Json, ScanOverwritePolicy.Overwrite);
        var secondBytes = File.ReadAllBytes(Path.Combine(_outputDirectory, "report.json"));

        Assert.Equal(firstBytes, secondBytes);
    }
}
