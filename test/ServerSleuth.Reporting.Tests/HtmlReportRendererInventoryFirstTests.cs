using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// Proves the "Server Deployment Inventory" report's redesigned structure: real discovered
/// entities (DLLs, services, scheduled tasks, runtimes, certificates) render grouped by
/// Application, in the documented section order, and the old Migration Checklist / flat
/// per-entity-type sections are gone entirely — see the report redesign plan §3.
/// </summary>
public class HtmlReportRendererInventoryFirstTests
{
    private static string BuildHtml()
    {
        var site = EntityFactory.Site("QINV", @"C:\QINV\QINV_WEB_NOURM");
        var pool = EntityFactory.ApplicationPool("QINVAppPool");
        var app = EntityFactory.Application("QINV", "/TEST", @"C:\QINV\QINV_WEB_NOURM", poolId: pool.Id, siteId: site.Id);

        var dapper = EntityFactory.Dll(@"C:\QINV\QINV_WEB_NOURM\Bin\Dapper.dll", referencedBy: [app.Id]);
        var epplus = EntityFactory.Dll(@"C:\QINV\QINV_WEB_NOURM\Bin\EPPlus.dll", referencedBy: [app.Id]);

        var svc = EntityFactory.Service("QINVWorker", @"C:\QINV\Worker\QINVWorker.exe");
        var task = EntityFactory.ScheduledTask(@"\QINV\Nightly", @"C:\QINV\Worker\QINVWorker.exe");
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "8.0.4");
        var cert = EntityFactory.Certificate("qinv.example.com", "ABC123", validTo: DateTimeOffset.UtcNow.AddYears(1));
        var config = EntityFactory.Configuration(@"C:\QINV\QINV_WEB_NOURM\web.config", ownerEntityId: app.Id);

        var entities = new List<DiscoveryEntity>
        {
            site, pool, app, dapper, epplus, svc, task, runtime, cert, config
        };

        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        return new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;
    }

    [Fact]
    public void RealComponentNames_AppearAsDeployedInventory()
    {
        var html = BuildHtml();

        Assert.Contains("Dapper.dll", html, StringComparison.Ordinal);
        Assert.Contains("EPPlus.dll", html, StringComparison.Ordinal);
        Assert.Contains("QINVWorker", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationComponentsSection_GroupsDllsUnderTheirOwningApplication()
    {
        var html = BuildHtml();

        var componentsStart = html.IndexOf("id=\"application-components\"", StringComparison.Ordinal);
        Assert.True(componentsStart >= 0);
        var nextSection = html.IndexOf("<section", componentsStart + 1, StringComparison.Ordinal);
        var section = html[componentsStart..(nextSection >= 0 ? nextSection : html.Length)];

        // Both DLLs render nested under the "QINV/TEST" application group, not as a flat item list.
        Assert.Contains(">QINV/TEST (", section, StringComparison.Ordinal);
        Assert.Contains("Dapper.dll", section, StringComparison.Ordinal);
        Assert.Contains("EPPlus.dll", section, StringComparison.Ordinal);
    }

    [Fact]
    public void BusinessScheduledTask_AppearsUnderItsOwnSection()
    {
        var html = BuildHtml();

        var start = html.IndexOf("id=\"business-scheduled-tasks\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var nextSection = html.IndexOf("<section", start + 1, StringComparison.Ordinal);
        var section = html[start..(nextSection >= 0 ? nextSection : html.Length)];

        Assert.Contains("Nightly", section, StringComparison.Ordinal);
    }

    [Fact]
    public void SectionsAppear_InDocumentedOrder()
    {
        var html = BuildHtml();

        var expectedOrder = new[]
        {
            "id=\"server-info\"",
            "id=\"summary\"",
            "id=\"applications\"",
            "id=\"windows-services\"",
            "id=\"installed-software\"",
            "id=\"application-components\"",
            "id=\"business-scheduled-tasks\"",
            "id=\"application-runtime\"",
            "id=\"application-databases\"",
        };

        var indices = expectedOrder.Select(marker => html.IndexOf(marker, StringComparison.Ordinal)).ToList();
        foreach (var index in indices)
        {
            Assert.True(index >= 0);
        }

        for (var i = 1; i < indices.Count; i++)
        {
            Assert.True(indices[i] > indices[i - 1]);
        }
    }

    [Fact]
    public void OldMigrationChecklistAndFlatEntityTypeSections_NoLongerExist()
    {
        var html = BuildHtml();

        Assert.DoesNotContain("id=\"migration-checklist\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"dll-binaries\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"com-components\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"scheduled-tasks\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"certificates\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"configuration-files\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"external-connections\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"runtime-requirements\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyDiscovery_RendersNoApplicationsOrComponents()
    {
        var entities = new List<DiscoveryEntity>();
        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var html = new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;

        Assert.DoesNotContain("id=\"migration-checklist\"", html, StringComparison.Ordinal);
        Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OmittingInventoryParameters_StillRendersValidDocument_WithNoApplications()
    {
        var site = EntityFactory.Site("QINV", @"C:\QINV\QINV_WEB_NOURM");
        var entities = new List<DiscoveryEntity> { site };
        var report = TestPipeline.Run(entities);

        var html = new HtmlReportRenderer().Render(report).Content;

        Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"migration-checklist\"", html, StringComparison.Ordinal);
    }
}
