using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Export;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests.Export;

/// <summary>
/// Runs the exact same 17-entity ERP fixture used by every prior phase's own fixture tests
/// through the real Phase 8C pipeline, exports both formats to disk, and reads them back — see
/// skill.md (Phase 9C) §21. Values are never hard-coded in exporter code; every assertion reads
/// the actual exported file content.
/// </summary>
public class LocalFileExportErpFixtureTests
{
    private static (string JsonContent, string HtmlContent) ExportAndRead(TempDirectory temp)
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

        var jsonResult = new JsonReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: []).Render(report);
        var htmlResult = new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: []).Render(report);
        var bundle = new ReportBundle
        {
            Json = ReportArtifactFactory.FromRenderResult(jsonResult, ReportArtifactFactory.DefaultJsonFileName),
            Html = ReportArtifactFactory.FromRenderResult(htmlResult, ReportArtifactFactory.DefaultHtmlFileName)
        };
        var result = new LocalFileReportExporter().ExportBundle(bundle, temp.Path);

        Assert.True(result.Success);

        return (System.IO.File.ReadAllText(result.Json.OutputPath!), System.IO.File.ReadAllText(result.Html.OutputPath!));
    }

    [Fact]
    public void ExportedFiles_Exist_AndHtmlShowsDeployedApplications_NotRiskStatus()
    {
        using var temp = new TempDirectory();
        var (json, html) = ExportAndRead(temp);

        Assert.True(System.IO.File.Exists(Path.Combine(temp.Path, "report.json")));
        Assert.True(System.IO.File.Exists(Path.Combine(temp.Path, "report.html")));

        // JSON still carries the full Risk/Migration assessment internally (unaffected by the
        // HTML report redesign — only the HTML renderer stopped surfacing it).
        Assert.Contains("\"OverallMigrationStatus\": \"Blocked\"", json, StringComparison.Ordinal);
        Assert.Contains("boundary:iis-application:ERP:/", json, StringComparison.Ordinal);

        // HTML shows the deployed applications, never the Risk/Migration status badges.
        Assert.Contains(">ERP<", html, StringComparison.Ordinal);
        Assert.Contains(">ERPWorker<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("badge status-", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedHostExe_ThreeSeparateAnchors_AllAppearAsApplicationsInHtml()
    {
        using var temp = new TempDirectory();
        var (json, html) = ExportAndRead(temp);

        // JSON: exactly one SharedInfrastructure entry with 3 boundaries (unaffected internal
        // Migration-analysis semantics).
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var shared = Assert.Single(doc.RootElement.GetProperty("SharedInfrastructure").EnumerateArray());
        Assert.Equal(3, shared.GetProperty("AffectedBoundaryIds").GetArrayLength());

        // HTML: all three anchors (not merged, since 3+ sharers are never merged) still show up
        // as their own deployed applications.
        Assert.Contains(">BatchA<", html, StringComparison.Ordinal);
        Assert.Contains(">BatchB<", html, StringComparison.Ordinal);
        Assert.Contains("BatchC", html, StringComparison.Ordinal);
    }
}
