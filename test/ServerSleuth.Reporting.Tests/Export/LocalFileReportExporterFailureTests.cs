using ServerSleuth.Reporting.Export;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests.Export;

/// <summary>Expected failure handling — see skill.md (Phase 9C) §17. Failures are represented as
/// a result, never a thrown exception for expected I/O conditions, and success is never claimed
/// when the final file doesn't exist.</summary>
public class LocalFileReportExporterFailureTests
{
    [Fact]
    public void InvalidOutputDirectory_CollidingWithAnExistingFile_FailsGracefully()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.GetDirectoryName(temp.Path)!);
        // Create a plain FILE at the path we're about to ask the exporter to treat as a directory.
        File.WriteAllText(temp.Path, "not a directory");

        try
        {
            var report = TestPipeline.Run([]);
            var bundle = ReportArtifactFactory.CreateBundle(report);

            var result = new LocalFileReportExporter().Export(bundle.Json, temp.Path);

            Assert.False(result.Success);
            Assert.Null(result.OutputPath);
            Assert.NotEmpty(result.Diagnostics);
        }
        finally
        {
            File.Delete(temp.Path);
        }
    }

    [Fact]
    public void NullArtifact_ThrowsArgumentNullException_NotSwallowed()
    {
        using var temp = new TempDirectory();
        Assert.Throws<ArgumentNullException>(() => new LocalFileReportExporter().Export(null!, temp.Path));
    }

    [Fact]
    public void NullOrEmptyOutputDirectory_ThrowsArgumentException()
    {
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        Assert.Throws<ArgumentException>(() => new LocalFileReportExporter().Export(bundle.Json, ""));
        Assert.ThrowsAny<ArgumentException>(() => new LocalFileReportExporter().Export(bundle.Json, null!));
    }

    [Fact]
    public void ExportBundle_BothArtifactsFailIndependently_WhenOneHasAnUnsafeFileName()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var goodBundle = ReportArtifactFactory.CreateBundle(report);

        var poisonedBundle = new ReportBundle
        {
            Json = goodBundle.Json with { FileName = "../escaped.json" },
            Html = goodBundle.Html
        };

        var result = new LocalFileReportExporter().ExportBundle(poisonedBundle, temp.Path);

        Assert.False(result.Json.Success);
        Assert.True(result.Html.Success);
        Assert.False(result.Success);
    }
}
