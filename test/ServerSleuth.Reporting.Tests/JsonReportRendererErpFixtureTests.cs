using System.Text.Json;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// Renders the exact same 17-entity ERP fixture used by every prior phase's own fixture tests,
/// run through the real Phase 8C pipeline, then through <see cref="JsonReportRenderer"/> — see
/// skill.md (Phase 9A) §14, §22. Asserts the JSON reflects the actual, previously-established
/// semantics (Server Blocked, ERP Web NeedsRemediation, ERPWorker Blocked, BatchA/B/C each
/// ReadyWithConditions, one shared `host.exe` dependency spanning 3 boundaries) — never
/// recalculated here, only serialized.
/// </summary>
public class JsonReportRendererErpFixtureTests
{
    private static (ServerMigrationAssessmentReport Report, JsonDocument Json) BuildAndRender()
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
        var result = new JsonReportRenderer().Render(report);
        return (report, JsonDocument.Parse(result.Content));
    }

    [Fact]
    public void ServerSummary_ReflectsBlockedStatus()
    {
        var (_, json) = BuildAndRender();
        var server = json.RootElement.GetProperty("Server");

        Assert.Equal("Blocked", server.GetProperty("OverallMigrationStatus").GetString());
        Assert.Equal(1, server.GetProperty("BlockingIssueCount").GetInt32());
        Assert.Equal(4, server.GetProperty("RemediationIssueCount").GetInt32());
        Assert.Equal(3, server.GetProperty("ConditionalDependencyCount").GetInt32());
        Assert.Equal(5, server.GetProperty("ApplicationCount").GetInt32());
    }

    [Fact]
    public void Applications_ReflectPreviouslyEstablishedStatuses()
    {
        var (_, json) = BuildAndRender();
        var apps = json.RootElement.GetProperty("Applications").EnumerateArray().ToList();

        var web = apps.Single(a => a.GetProperty("BoundaryId").GetString() == "boundary:iis-application:ERP:/");
        Assert.Equal("NeedsRemediation", web.GetProperty("MigrationStatus").GetString());

        var worker = apps.Single(a => a.GetProperty("BoundaryId").GetString() == "boundary:service:ERPWorker");
        Assert.Equal("Blocked", worker.GetProperty("MigrationStatus").GetString());

        foreach (var boundaryId in new[] { "boundary:service:BatchA", "boundary:service:BatchB", "boundary:scheduledtask:\\ERP\\BatchC" })
        {
            var batch = apps.Single(a => a.GetProperty("BoundaryId").GetString() == boundaryId);
            Assert.Equal("ReadyWithConditions", batch.GetProperty("MigrationStatus").GetString());
        }
    }

    [Fact]
    public void SharedHostExe_IsOneLogicalDependency_WithThreeAffectedBoundaries()
    {
        var (_, json) = BuildAndRender();
        var shared = json.RootElement.GetProperty("SharedInfrastructure").EnumerateArray().ToList();

        var hostExe = Assert.Single(shared, d => d.GetProperty("Type").GetString() == "SharedBinary");
        var boundaries = hostExe.GetProperty("AffectedBoundaryIds").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(3, boundaries.Count);
    }

    [Fact]
    public void Dependencies_AreGroupedByAllFiveExpectedTypes()
    {
        var (_, json) = BuildAndRender();
        var groups = json.RootElement.GetProperty("Dependencies").EnumerateArray().Select(g => g.GetProperty("Type").GetString()).OrderBy(t => t, StringComparer.Ordinal).ToList();

        Assert.Equal(new[] { "Certificate", "Database", "FileShare", "Runtime", "SharedBinary" }.OrderBy(t => t, StringComparer.Ordinal), groups);
    }

    [Fact]
    public void Actions_PreserveRuleIdAndEvidenceProvenance()
    {
        var (_, json) = BuildAndRender();
        var actions = json.RootElement.GetProperty("Actions").EnumerateArray().ToList();

        Assert.Equal(8, actions.Count);
        Assert.All(actions, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.GetProperty("ActionId").GetString()));
            Assert.NotEmpty(a.GetProperty("RelatedIssueIds").EnumerateArray());
        });
    }

    [Fact]
    public void PreAndPostMigrationChecks_ArePresent()
    {
        var (_, json) = BuildAndRender();
        Assert.NotEmpty(json.RootElement.GetProperty("PreMigrationChecks").EnumerateArray());
        Assert.NotEmpty(json.RootElement.GetProperty("PostMigrationChecks").EnumerateArray());
    }

    [Fact]
    public void Diagnostics_ReflectActualConsolidationCounts()
    {
        var (report, json) = BuildAndRender();
        var diagnostics = json.RootElement.GetProperty("Diagnostics");

        Assert.Equal(report.Diagnostics.ApplicationsConsolidated, diagnostics.GetProperty("ApplicationsConsolidated").GetInt32());
        Assert.Equal(report.Diagnostics.SharedInfrastructureDependencyCount, diagnostics.GetProperty("SharedInfrastructureDependencyCount").GetInt32());
    }

    [Fact]
    public void EvidenceAndConfidence_ArePreservedOnIssues()
    {
        var (_, json) = BuildAndRender();
        var worker = json.RootElement.GetProperty("Applications").EnumerateArray()
            .Single(a => a.GetProperty("BoundaryId").GetString() == "boundary:service:ERPWorker");
        var issue = worker.GetProperty("Issues").EnumerateArray().Single();

        Assert.False(string.IsNullOrWhiteSpace(issue.GetProperty("RuleId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(issue.GetProperty("SourceRiskFindingId").GetString()));
        Assert.NotEmpty(issue.GetProperty("Evidence").EnumerateArray());
        Assert.True(issue.GetProperty("Confidence").GetProperty("Value").GetDouble() is >= 0 and <= 1);
    }
}
