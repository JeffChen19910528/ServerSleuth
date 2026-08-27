using ServerSleuth.Reporting.Export;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests.Export;

/// <summary>Basic export behavior, default filenames, and overwrite policy — see skill.md
/// (Phase 9C) §5, §8, §20.</summary>
public class LocalFileReportExporterTests
{
    [Fact]
    public void Export_WritesArtifactContentExactly_UsesDefaultFileName()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        var result = new LocalFileReportExporter().Export(bundle.Json, temp.Path);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(temp.Path, "report.json"), result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
        Assert.Equal(bundle.Json.Content, File.ReadAllText(result.OutputPath!, bundle.Json.Encoding));
        Assert.Equal(bundle.Json.ContentLength, result.BytesWritten);
    }

    [Fact]
    public void ExportBundle_WritesBothDefaultFileNames()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        var result = new LocalFileReportExporter().ExportBundle(bundle, temp.Path);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(temp.Path, "report.json")));
        Assert.True(File.Exists(Path.Combine(temp.Path, "report.html")));
        Assert.Null(result.Manifest);
    }

    [Fact]
    public void Export_CreatesOutputDirectory_WhenItDoesNotExist()
    {
        using var temp = new TempDirectory();
        var nested = Path.Combine(temp.Path, "nested", "output");
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        var result = new LocalFileReportExporter().Export(bundle.Json, nested);

        Assert.True(result.Success);
        Assert.True(Directory.Exists(nested));
    }

    [Fact]
    public void FailIfExists_IsTheDefaultPolicy_AndFailsOnSecondExport()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);
        var exporter = new LocalFileReportExporter();

        var first = exporter.Export(bundle.Json, temp.Path);
        var second = exporter.Export(bundle.Json, temp.Path);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Null(second.OutputPath);
        Assert.NotEmpty(second.Diagnostics);
    }

    [Fact]
    public void Overwrite_ReplacesExistingFile_Successfully()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);
        var exporter = new LocalFileReportExporter();

        var first = exporter.Export(bundle.Json, temp.Path);
        var second = exporter.Export(bundle.Json, temp.Path, ReportOverwritePolicy.Overwrite);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(bundle.Json.Content, File.ReadAllText(second.OutputPath!, bundle.Json.Encoding));
    }

    [Fact]
    public void Export_NeverLeavesATemporaryFileBehind_OnSuccess()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        new LocalFileReportExporter().Export(bundle.Json, temp.Path);

        var entries = Directory.GetFiles(temp.Path);
        Assert.Single(entries);
        Assert.EndsWith("report.json", entries[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Export_NeverLeavesATemporaryFileBehind_OnFailIfExistsFailure()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);
        var exporter = new LocalFileReportExporter();

        exporter.Export(bundle.Json, temp.Path);
        exporter.Export(bundle.Json, temp.Path); // expected failure

        var entries = Directory.GetFiles(temp.Path);
        Assert.Single(entries); // only the original report.json — no stray .tmp file
    }

    [Fact]
    public void Export_ReportsSuccess_OnlyWhenFileActuallyExists()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        var result = new LocalFileReportExporter().Export(bundle.Json, temp.Path);

        Assert.Equal(result.Success, File.Exists(result.OutputPath));
    }
}
