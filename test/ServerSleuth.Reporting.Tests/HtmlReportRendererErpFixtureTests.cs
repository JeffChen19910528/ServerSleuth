using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// Renders the exact same 17-entity ERP fixture used by every prior phase's own fixture tests,
/// run through the real Phase 8C pipeline, then through <see cref="HtmlReportRenderer"/> — see
/// skill.md (Phase 9B) §24. Asserts the HTML reflects the actual, previously-established
/// semantics — never recalculated here, only rendered.
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

        var report = TestPipeline.Run(entities);
        return new HtmlReportRenderer().Render(report).Content;
    }

    [Fact]
    public void ContainsWellFormedHtmlDocument()
    {
        var html = BuildHtml();
        Assert.StartsWith("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("</html>", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContainsBlockedServerStatus()
    {
        var html = BuildHtml();
        Assert.Contains("badge status-blocked", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainsErpWebNeedsRemediation()
    {
        var html = BuildHtml();
        Assert.Contains("boundary:iis-application:ERP:/", html, StringComparison.Ordinal);
        Assert.Contains("badge status-needs-remediation", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainsErpWorkerBlocked()
    {
        var html = BuildHtml();
        Assert.Contains("boundary:service:ERPWorker", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainsAllThreeBatchBoundaries_ReadyWithConditions()
    {
        var html = BuildHtml();
        Assert.Contains("boundary:service:BatchA", html, StringComparison.Ordinal);
        Assert.Contains("boundary:service:BatchB", html, StringComparison.Ordinal);
        Assert.Contains("boundary:scheduledtask:\\ERP\\BatchC", html.Replace("&#x5C;", "\\"), StringComparison.Ordinal);
        Assert.Contains("badge status-ready-with-conditions", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedHostExe_RendersAsOneLogicalDependency_WithThreeAffectedBoundaries()
    {
        var html = BuildHtml();
        var sharedSectionStart = html.IndexOf("id=\"shared-infrastructure\"", StringComparison.Ordinal);
        Assert.True(sharedSectionStart >= 0);

        var nextSectionStart = html.IndexOf("<section", sharedSectionStart + 1, StringComparison.Ordinal);
        var sharedSection = html[sharedSectionStart..nextSectionStart];

        // One dependency row (one <code>dependency:SharedBinary:...</code> occurrence), not three.
        var occurrences = System.Text.RegularExpressions.Regex.Matches(sharedSection, "dependency:SharedBinary:").Count;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void ContainsDependencyTypeGroups()
    {
        var html = BuildHtml();
        foreach (var type in new[] { "Certificate", "Database", "FileShare", "Runtime", "SharedBinary" })
        {
            Assert.Contains($">{type} (", html, StringComparison.Ordinal);
        }
    }
}
