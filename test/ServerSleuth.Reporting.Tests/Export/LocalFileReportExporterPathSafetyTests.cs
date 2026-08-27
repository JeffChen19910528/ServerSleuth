using System.Text;
using ServerSleuth.Reporting.Export;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests.Export;

/// <summary>Path/filename safety — see skill.md (Phase 9C) §7. A malicious or malformed
/// <c>ReportArtifact.FileName</c> must never escape the caller's output directory.</summary>
public class LocalFileReportExporterPathSafetyTests
{
    private static ReportArtifact ArtifactWithFileName(string fileName) => new()
    {
        FileName = fileName,
        Format = ReportFormat.Json,
        Content = "{}",
        Encoding = Encoding.UTF8,
        ContentLength = 2
    };

    [Theory]
    [InlineData("../../evil")]
    [InlineData("..\\..\\evil")]
    [InlineData("C:\\evil")]
    [InlineData("/var/tmp/evil")]
    [InlineData("<script>")]
    [InlineData("server:name")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("report\u0000.json")]
    public void Export_RejectsMaliciousOrMalformedFileName_NeverWritesOutsideOutputDirectory(string maliciousFileName)
    {
        using var temp = new TempDirectory();
        var artifact = ArtifactWithFileName(maliciousFileName);

        var result = new LocalFileReportExporter().Export(artifact, temp.Path);

        Assert.False(result.Success);
        Assert.Null(result.OutputPath);
        Assert.NotEmpty(result.Diagnostics);

        // Nothing was written anywhere inside the (created but otherwise empty) output directory.
        if (Directory.Exists(temp.Path))
        {
            Assert.Empty(Directory.GetFiles(temp.Path));
        }
    }

    [Fact]
    public void MaliciousFileName_NeverEscapesToAParentDirectory()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Path);
        var parentBefore = Directory.GetFiles(Path.GetDirectoryName(temp.Path.TrimEnd(Path.DirectorySeparatorChar))!);

        var artifact = ArtifactWithFileName("../escaped-report.json");
        new LocalFileReportExporter().Export(artifact, temp.Path);

        var parentAfter = Directory.GetFiles(Path.GetDirectoryName(temp.Path.TrimEnd(Path.DirectorySeparatorChar))!);
        Assert.Equal(parentBefore.Length, parentAfter.Length);
    }

    [Fact]
    public void ReportArtifactFactory_RejectsUnsafePrefix()
    {
        var report = TestPipeline.Run([]);
        Assert.Throws<ArgumentException>(() => ReportArtifactFactory.CreateBundle(report, filePrefix: "../evil"));
    }

    [Fact]
    public void ReportArtifactFactory_AcceptsSafePrefix()
    {
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report, filePrefix: "my-server-2026");

        Assert.Equal("my-server-2026.json", bundle.Json.FileName);
        Assert.Equal("my-server-2026.html", bundle.Html.FileName);
    }
}
