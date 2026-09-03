using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// Basic rendering contract for the Server Deployment Inventory report. Migration Actions,
/// Verification Checks, per-entity Evidence lists, and Risk Issues are Risk/Migration assessment
/// content and are deliberately never rendered here — see the report redesign plan.
/// </summary>
public class HtmlReportRendererBasicTests
{
    [Fact]
    public void Format_IsHtml()
    {
        Assert.Equal(ReportFormat.Html, new HtmlReportRenderer().Format);
    }

    [Fact]
    public void Render_ReturnsHtmlFormat_WithUtf8Encoding()
    {
        var report = TestPipeline.Run([]);
        var result = new HtmlReportRenderer().Render(report);

        Assert.Equal(ReportFormat.Html, result.Format);
        Assert.Equal(System.Text.Encoding.UTF8, result.Encoding);
    }

    [Fact]
    public void RiskAndMigrationAssessmentContent_IsNeverRendered()
    {
        var service = EntityFactory.Service("BasicSvc", @"D:\Basic\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Basic\svc.exe", notFound: true);
        var expiring = EntityFactory.Certificate("basic.example.com", "BASICCERT", validTo: DateTimeOffset.UtcNow.AddDays(10));

        var entities = new List<DiscoveryEntity> { service, missingExe, expiring };
        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var html = new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;

        Assert.DoesNotContain("Pre-Migration", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Post-Migration", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Review / Documentation", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"evidence-list\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("badge severity-", html, StringComparison.Ordinal);
        Assert.DoesNotContain("badge impact-", html, StringComparison.Ordinal);
        Assert.DoesNotContain("RuleId", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceInventory_StillRendersTheDeployedService()
    {
        var service = EntityFactory.Service("EvidSvc", @"D:\Evid\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Evid\svc.exe", notFound: true);

        var entities = new List<DiscoveryEntity> { service, missingExe };
        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var html = new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;

        Assert.Contains(">EvidSvc<", html, StringComparison.Ordinal);
    }
}
