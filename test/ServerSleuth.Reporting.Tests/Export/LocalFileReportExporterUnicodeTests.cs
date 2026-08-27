using ServerSleuth.Reporting.Export;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests.Export;

/// <summary>UTF-8/Unicode on export — see skill.md (Phase 9C) §13.</summary>
public class LocalFileReportExporterUnicodeTests
{
    [Theory]
    [InlineData("繁體中文")]
    [InlineData("日本語")]
    [InlineData("한국어")]
    [InlineData("café")]
    public void ExportedFileContent_PreservesUnicodeApplicationName(string unicodeName)
    {
        using var temp = new TempDirectory();
        var site = EntityFactory.Site(unicodeName);
        var app = EntityFactory.Application(unicodeName, "/", @"D:\App", siteId: site.Id);
        var webDll = EntityFactory.Dll(@"D:\App\web.dll", referencedBy: [app.Id], importsCsv: "missing.dll");
        var missingDll = EntityFactory.Dll(@"D:\App\missing.dll", notFound: true);

        var entities = new List<Core.Models.DiscoveryEntity> { site, app, webDll, missingDll };
        var report = TestPipeline.Run(entities);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        var result = new LocalFileReportExporter().ExportBundle(bundle, temp.Path);

        Assert.True(result.Success);
        var jsonContent = File.ReadAllText(result.Json.OutputPath!, System.Text.Encoding.UTF8);
        var htmlContent = File.ReadAllText(result.Html.OutputPath!, System.Text.Encoding.UTF8);

        Assert.Contains(unicodeName, jsonContent, StringComparison.Ordinal);
        Assert.Contains(unicodeName, htmlContent, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputDirectory_WithUnicodePath_WorksCorrectly()
    {
        using var temp = new TempDirectory();
        var unicodeSubdir = Path.Combine(temp.Path, "中文路徑", "報告輸出");

        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        var result = new LocalFileReportExporter().ExportBundle(bundle, unicodeSubdir);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(unicodeSubdir, "report.json")));
        Assert.True(File.Exists(Path.Combine(unicodeSubdir, "report.html")));
    }
}
