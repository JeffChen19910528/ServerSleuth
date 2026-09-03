using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>Unicode safety — see skill.md (Phase 9B) §14: Traditional Chinese and mixed-Unicode
/// names/paths must render without corruption.</summary>
public class HtmlReportRendererUnicodeTests
{
    [Fact]
    public void TraditionalChineseApplicationName_RendersLiterally_Uncorrupted()
    {
        const string chineseName = "伺服器遷移評估";
        var site = EntityFactory.Site(chineseName, @"D:\伺服器\Web");
        var app = EntityFactory.Application(chineseName, "/", @"D:\伺服器\Web", siteId: site.Id);
        var webDll = EntityFactory.Dll(@"D:\伺服器\Web\主程式.dll", referencedBy: [app.Id], importsCsv: "缺少的檔案.dll");
        var missingDll = EntityFactory.Dll(@"D:\伺服器\Web\缺少的檔案.dll", notFound: true);

        var entities = new List<DiscoveryEntity> { site, app, webDll, missingDll };
        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var html = new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;

        Assert.Contains(chineseName, html, StringComparison.Ordinal);
        Assert.Contains("伺服器", html, StringComparison.Ordinal);
    }

    [Fact]
    public void MixedEnglishAndUnicodePaths_RenderUncorrupted()
    {
        const string mixedPath = @"D:\Ünïcödé Path 路徑\app.exe";
        var service = EntityFactory.Service("MixedSvc", mixedPath);
        var missingExe = EntityFactory.Dll(mixedPath, notFound: true);

        var entities = new List<DiscoveryEntity> { service, missingExe };
        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var html = new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;

        Assert.Contains("路徑", html, StringComparison.Ordinal);
        Assert.Contains("Ünïcödé", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderedContent_DeclaresUtf8Charset()
    {
        var report = TestPipeline.Run([]);
        var html = new HtmlReportRenderer().Render(report).Content;

        Assert.Contains("<meta charset=\"utf-8\">", html, StringComparison.Ordinal);
    }
}
