using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>HTML escaping — see skill.md (Phase 9B) §18: every piece of dynamic text must be
/// HTML-encoded, so discovered data can never break the document or inject markup/script.</summary>
public class HtmlReportRendererHtmlEscapingTests
{
    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("\"><img src=x onerror=alert(1)>")]
    [InlineData("</script><script>evil()</script>")]
    [InlineData("Tom & Jerry's \"Service\" <Prod>")]
    public void HtmlSpecialCharactersInApplicationName_AreEncoded_NeverBreakDocument(string dangerousName)
    {
        var site = EntityFactory.Site(dangerousName);
        var app = EntityFactory.Application(dangerousName, "/", @"D:\App", siteId: site.Id);
        var webDll = EntityFactory.Dll(@"D:\App\web.dll", referencedBy: [app.Id], importsCsv: "missing.dll");
        var missingDll = EntityFactory.Dll(@"D:\App\missing.dll", notFound: true);

        var entities = new List<DiscoveryEntity> { site, app, webDll, missingDll };
        var report = TestPipeline.Run(entities);
        var html = new HtmlReportRenderer().Render(report).Content;

        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img src=x onerror=alert(1)>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<Prod>", html, StringComparison.Ordinal);

        // The escaped form of the dangerous name must actually be present — proving the data
        // reached the document (rather than being silently dropped) while remaining inert.
        Assert.Contains("&lt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlDocument_RemainsWellFormed_WithHostileEvidenceDetail()
    {
        // Evidence Detail (ExecutablePath) deliberately shaped to try to break out of an
        // attribute/tag context.
        var dangerousPath = "D:\\App\\\"><script>document.location='https://evil.example/'</script>\\svc.exe";
        var service = EntityFactory.Service("EscSvc", dangerousPath);
        var missingExe = EntityFactory.Dll(dangerousPath, notFound: true);

        var entities = new List<DiscoveryEntity> { service, missingExe };
        var report = TestPipeline.Run(entities);
        var html = new HtmlReportRenderer().Render(report).Content;

        Assert.DoesNotContain("<script>document.location", html, StringComparison.Ordinal);
        Assert.Contains("&quot;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void NoScriptTagIsEverEmitted()
    {
        var report = TestPipeline.Run([]);
        var html = new HtmlReportRenderer().Render(report).Content;

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }
}
