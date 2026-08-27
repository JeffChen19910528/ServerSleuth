using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>Raw configuration non-disclosure for the HTML renderer — see skill.md (Phase 9B)
/// §17. <c>Configuration</c> has never carried raw file content (Phase 4E-1/6E); this renderer
/// adds no new source of raw content on top of that.</summary>
public class HtmlReportRendererRawConfigurationSafetyTests
{
    [Fact]
    public void RenderedHtml_NeverContainsXmlConfigurationMarkup()
    {
        var app = EntityFactory.Application("XmlApp", "/", @"D:\XmlApp");
        var config = EntityFactory.Configuration(@"D:\XmlApp\web.config", ownerEntityId: app.Id,
            dependencyReferences: ["FileShare: \\\\FILESERVER\\Share"]);

        var entities = new List<DiscoveryEntity> { app, config };
        var report = TestPipeline.Run(entities);
        var html = new HtmlReportRenderer().Render(report).Content;

        Assert.DoesNotContain("&lt;configuration&gt;", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<configuration>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system.webServer", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionStrings", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderedHtml_NeverContainsSystemdOrDockerRawUnitContent()
    {
        var service = EntityFactory.Service("UnitSvc", @"D:\Unit\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Unit\svc.exe", notFound: true);

        var entities = new List<DiscoveryEntity> { service, missingExe };
        var report = TestPipeline.Run(entities);
        var html = new HtmlReportRenderer().Render(report).Content;

        Assert.DoesNotContain("[Unit]", html, StringComparison.Ordinal);
        Assert.DoesNotContain("[Service]", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecStart=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Image\":", html, StringComparison.Ordinal);
    }
}
