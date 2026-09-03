using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// Renders the same 17-entity ERP fixture used by every prior phase's own fixture tests, run
/// through the real pipeline and the "Server Deployment Inventory" HTML renderer. The report no
/// longer shows Risk/Migration content (severity, blocking issues, coverage) — these tests assert
/// what the redesigned inventory-first report actually shows (deployed applications) and confirm
/// migration/risk terminology is gone.
/// </summary>
public class HtmlReportRendererErpFixtureTests
{
    private static string BuildHtml()
    {
        var site = EntityFactory.Site("ERP", @"D:\ERP\Web");
        var pool = EntityFactory.ApplicationPool("ERPAppPool");
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP\Web", poolId: pool.Id, siteId: site.Id);

        var webDll = EntityFactory.Dll(@"D:\ERP\Web\ERP.Web.dll", referencedBy: [app.Id], importsCsv: "VendorImport.dll");
        var missingImportDll = EntityFactory.Dll(@"D:\ERP\Web\VendorImport.dll", notFound: true);

        var appConfig = EntityFactory.Configuration(@"D:\ERP\Web\web.config", ownerEntityId: app.Id,
            dependencyReferences: ["RuntimeVersion: net8.0"]);
        appConfig.SetMetadata("ParseStatus", "AccessDenied");
        appConfig.SetMetadata("Database0.Type", "SqlServer");
        appConfig.SetMetadata("Database0.Host", "DB01");
        appConfig.SetMetadata("Database0.Port", "1433");
        appConfig.SetMetadata("Database0.Name", "ERP");
        appConfig.SetMetadata("NetworkPath0.Server", "FILESERVER");
        appConfig.SetMetadata("NetworkPath0.Share", "ERPData");

        var runtime6 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "6.0.30");
        var runtime10 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");

        EntityFactory.SetBinding(site, 0, "EXPIRING123");
        var expiringCert = EntityFactory.Certificate("erp.example.com", "EXPIRING123", validTo: DateTimeOffset.UtcNow.AddDays(10));

        var service = EntityFactory.Service("ERPWorker", @"D:\ERP\Worker\ERPWorker.exe");
        var missingWorkerExe = EntityFactory.Dll(@"D:\ERP\Worker\ERPWorker.exe", notFound: true);

        var batchA = EntityFactory.Service("BatchA", @"D:\ERP\Shared\host.exe");
        var batchB = EntityFactory.Service("BatchB", @"D:\ERP\Shared\host.exe");
        var batchC = EntityFactory.ScheduledTask(@"\ERP\BatchC", @"D:\ERP\Shared\host.exe");
        var sharedHostExe = EntityFactory.Dll(@"D:\ERP\Shared\host.exe");

        var healthyDll = EntityFactory.Dll(@"D:\ERP\Web\Healthy.dll", referencedBy: [app.Id]);
        var healthyCert = EntityFactory.Certificate("unused.example.com", "HEALTHY999", validTo: DateTimeOffset.UtcNow.AddYears(2));

        var entities = new List<DiscoveryEntity>
        {
            site, pool, app,
            webDll, missingImportDll,
            appConfig,
            runtime6, runtime10,
            expiringCert,
            service, missingWorkerExe,
            batchA, batchB, batchC, sharedHostExe,
            healthyDll, healthyCert
        };

        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        return new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;
    }

    [Fact]
    public void ContainsWellFormedHtmlDocument()
    {
        var html = BuildHtml();
        Assert.StartsWith("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("</html>", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContainsDeployedApplicationNames()
    {
        var html = BuildHtml();
        Assert.Contains(">ERP<", html, StringComparison.Ordinal);
        Assert.Contains(">ERPWorker<", html, StringComparison.Ordinal);
        Assert.Contains(">BatchA<", html, StringComparison.Ordinal);
        Assert.Contains(">BatchB<", html, StringComparison.Ordinal);
        Assert.Contains("BatchC", html, StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotContainRiskOrMigrationTerminology()
    {
        var html = BuildHtml();
        Assert.DoesNotContain("status-blocked", html, StringComparison.Ordinal);
        Assert.DoesNotContain("status-needs-remediation", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Migration Status", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Severity", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Blocking", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Verification", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Coverage", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Confidence", html, StringComparison.Ordinal);
        Assert.DoesNotContain("shared-infrastructure", html, StringComparison.Ordinal);
        Assert.DoesNotContain("migration-checklist", html, StringComparison.Ordinal);
    }
}
