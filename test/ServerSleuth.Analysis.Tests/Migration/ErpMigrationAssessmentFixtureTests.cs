using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration;

/// <summary>
/// Runs the exact same 17-entity ERP fixture as Phase 7A's <c>ErpRiskFixtureTests</c>/Phase 7B's
/// <c>ErpRiskAggregationFixtureTests</c> (skill.md Phase 7A §30) all the way through
/// <see cref="MigrationAssessmentEngine"/> — see skill.md (Phase 8A) §19: "The exact result must
/// be derived from actual rule outputs. Do NOT hard-code the expected final migration status."
///
/// Observed (via a one-off exploratory probe against the real pipeline, since removed) and
/// reproduced by the assertions below: ERP Worker → Blocked (matches skill.md §19's own
/// example exactly); ERP Web → NeedsRemediation (skill.md §19 explicitly allows either
/// NeedsRemediation or ReadyWithConditions depending on actual output — NeedsRemediation is
/// what the real RR2/RR3/RR4 findings on this boundary, all High severity, actually produce);
/// BatchA/BatchB/BatchC → ReadyWithConditions each (the shared host.exe finding, Conditional,
/// fans out to all three per the Phase 7B hardening); Server → Blocked overall (a single
/// Blocking issue — ERPWorker's Critical missing executable — always wins), with 1 Blocking,
/// 4 RemediationRequired (RR2 native import + RR3 AccessDenied + RR4 MissingRuntime + RR5
/// CertificateExpiry), 3 Conditional (RR9 Database + RR9 FileShare + RR10 SharedInfrastructure),
/// 0 Informational, 0 Unclassified. 5 MigrationDependency records: Certificate, Database,
/// FileShare, Runtime, SharedBinary — one of each type this fixture's evidence actually supports.
/// </summary>
public class ErpMigrationAssessmentFixtureTests
{
    private static MigrationAssessmentSummary BuildAndAssess()
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

        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        return new MigrationAssessmentEngine().Assess(context, result, aggregation);
    }

    [Fact]
    public void ServerAssessment_MatchesActualObservedOutput()
    {
        var migration = BuildAndAssess();

        Assert.Equal(MigrationStatus.Blocked, migration.Server.OverallStatus);
        Assert.Equal(1, migration.Server.BlockingIssueCount);
        Assert.Equal(4, migration.Server.RemediationIssueCount);
        Assert.Equal(3, migration.Server.ConditionalDependencyCount);
        Assert.Equal(0, migration.Server.InformationalIssueCount);
        Assert.Equal(0, migration.Server.UnclassifiedIssueCount);
        Assert.Equal(5, migration.Server.ApplicationAssessments.Count);
        Assert.Equal(5, migration.Server.Dependencies.Count);
    }

    [Fact]
    public void ErpWebApplication_IsNeedsRemediation_WithThreeIssues()
    {
        var migration = BuildAndAssess();

        var web = Assert.Single(migration.Server.ApplicationAssessments, a => a.ApplicationBoundaryId == "boundary:iis-application:ERP:/");
        Assert.Equal(MigrationStatus.NeedsRemediation, web.OverallStatus);
        Assert.Equal(3, web.Issues.Count);
        Assert.Equal(3, web.RemediationIssueCount);
    }

    [Fact]
    public void ErpWorkerApplication_IsBlocked_WithOneIssue()
    {
        var migration = BuildAndAssess();

        var worker = Assert.Single(migration.Server.ApplicationAssessments, a => a.ApplicationBoundaryId == "boundary:service:ERPWorker");
        Assert.Equal(MigrationStatus.Blocked, worker.OverallStatus);
        Assert.Single(worker.Issues);
        Assert.Equal(1, worker.BlockingIssueCount);
    }

    [Fact]
    public void BatchBoundaries_AreAllReadyWithConditions_EachWithOneIssue()
    {
        var migration = BuildAndAssess();

        var batchBoundaries = new[] { "boundary:service:BatchA", "boundary:service:BatchB", "boundary:scheduledtask:\\ERP\\BatchC" };
        var batches = migration.Server.ApplicationAssessments.Where(a => batchBoundaries.Contains(a.ApplicationBoundaryId)).ToList();

        Assert.Equal(3, batches.Count);
        Assert.All(batches, b =>
        {
            Assert.Equal(MigrationStatus.ReadyWithConditions, b.OverallStatus);
            Assert.Single(b.Issues);
            Assert.Equal(1, b.ConditionalDependencyCount);
        });
    }

    [Fact]
    public void Dependencies_CoverAllFiveExpectedTypes()
    {
        var migration = BuildAndAssess();

        var types = migration.Server.Dependencies.Select(d => d.Type).OrderBy(t => t.ToString(), StringComparer.Ordinal).ToList();
        Assert.Equal(
            new[]
            {
                MigrationDependencyType.Certificate,
                MigrationDependencyType.Database,
                MigrationDependencyType.FileShare,
                MigrationDependencyType.Runtime,
                MigrationDependencyType.SharedBinary
            }.OrderBy(t => t.ToString(), StringComparer.Ordinal),
            types);
    }
}
