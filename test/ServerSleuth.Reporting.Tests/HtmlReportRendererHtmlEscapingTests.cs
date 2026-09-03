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
        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var html = new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;

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
        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var html = new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;

        Assert.DoesNotContain("<script>document.location", html, StringComparison.Ordinal);
        Assert.Contains("&quot;", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The report emits one static <c>&lt;script&gt;</c> block for search/filter UX — this is
    /// intentional. The security property being verified here is stronger: DYNAMIC user data
    /// (discovered names, paths, credentials) must never appear inside the script block itself,
    /// only in HTML attributes (where it is already HTML-encoded by <c>Esc()</c>). A hostile
    /// service name shaped to break out of a JS string context must remain outside the block.
    /// </summary>
    [Fact]
    public void DynamicUserData_NeverAppearsInsideScriptBlock()
    {
        // Adversarially shaped to break a JS string context if ever interpolated into script.
        var hostileName = "'; alert('XSS'); var x='";
        var service = EntityFactory.Service(hostileName, @"C:\Windows\system32\svchost.exe");
        var entities = new List<DiscoveryEntity> { service };
        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var html = new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;

        // Extract content of the script block (if present) and verify hostile data is absent.
        var scriptStart = html.IndexOf("<script>", StringComparison.OrdinalIgnoreCase);
        if (scriptStart >= 0)
        {
            var scriptEnd = html.IndexOf("</script>", scriptStart, StringComparison.OrdinalIgnoreCase);
            var scriptBlock = scriptEnd >= 0 ? html[scriptStart..scriptEnd] : html[scriptStart..];
            // The raw hostile string must NOT appear inside the script block.
            Assert.DoesNotContain(hostileName, scriptBlock, StringComparison.Ordinal);
            Assert.DoesNotContain("alert('XSS')", scriptBlock, StringComparison.Ordinal);
        }

        // The hostile string must still appear encoded somewhere in the document (not dropped).
        Assert.Contains("&#x27;", html, StringComparison.Ordinal);
    }
}
