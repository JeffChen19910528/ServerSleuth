using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration.Consolidation;

/// <summary>
/// Runs the exact same 17-entity ERP fixture as Phase 8A/8B's own fixtures, all the way through
/// <see cref="ServerMigrationAssessmentReportEngine"/> — see skill.md (Phase 8C) §18. Known-good
/// baseline (from <c>ErpMigrationAssessmentFixtureTests</c>/<c>ErpMigrationPlanFixtureTests</c>):
/// Server Blocked (1 Blocking, 4 RemediationRequired, 3 Conditional); ERP Web NeedsRemediation;
/// ERPWorker Blocked; BatchA/B/C each ReadyWithConditions; shared <c>host.exe</c> = one logical
/// SharedBinary dependency spanning all 3 Batch boundaries; 5 dependencies total (Certificate,
/// Database, FileShare, Runtime, SharedBinary); 8 actions (one per non-Informational issue).
/// </summary>
public class ErpMigrationReportFixtureTests
{
    private static ServerMigrationAssessmentReport BuildReport()
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
        var assessment = new MigrationAssessmentEngine().Assess(context, result, aggregation);
        var plan = MigrationPlanEngine.Plan(assessment);

        return ServerMigrationAssessmentReportEngine.Build(context, aggregation, assessment, plan);
    }

    [Fact]
    public void ServerSummary_MatchesActualObservedOutput()
    {
        var report = BuildReport();

        Assert.Equal(MigrationStatus.Blocked, report.ServerSummary.OverallMigrationStatus);
        Assert.Equal(1, report.ServerSummary.BlockingIssueCount);
        Assert.Equal(4, report.ServerSummary.RemediationIssueCount);
        Assert.Equal(3, report.ServerSummary.ConditionalDependencyCount);
        Assert.Equal(5, report.ServerSummary.ApplicationCount);
        Assert.Equal(1, report.ServerSummary.BlockedApplicationCount);
        Assert.Equal(1, report.ServerSummary.NeedsRemediationApplicationCount);
        Assert.Equal(3, report.ServerSummary.ReadyWithConditionsApplicationCount);
        Assert.Equal(0, report.ServerSummary.ReadyApplicationCount);
        Assert.Equal(5, report.ServerSummary.DependencyCount);
        Assert.Equal(8, report.ServerSummary.ActionCount);
    }

    [Fact]
    public void ErpWeb_IsNeedsRemediation_ErpWorker_IsBlocked()
    {
        var report = BuildReport();

        var web = Assert.Single(report.ApplicationAssessments, a => a.Assessment.ApplicationBoundaryId == "boundary:iis-application:ERP:/");
        Assert.Equal(MigrationStatus.NeedsRemediation, web.Assessment.OverallStatus);

        var worker = Assert.Single(report.ApplicationAssessments, a => a.Assessment.ApplicationBoundaryId == "boundary:service:ERPWorker");
        Assert.Equal(MigrationStatus.Blocked, worker.Assessment.OverallStatus);
        Assert.NotEmpty(worker.Actions);
    }

    [Fact]
    public void BatchBoundaries_AreAllReadyWithConditions()
    {
        var report = BuildReport();

        var batchIds = new[] { "boundary:service:BatchA", "boundary:service:BatchB", "boundary:scheduledtask:\\ERP\\BatchC" };
        var batches = report.ApplicationAssessments.Where(a => batchIds.Contains(a.Assessment.ApplicationBoundaryId)).ToList();

        Assert.Equal(3, batches.Count);
        Assert.All(batches, b => Assert.Equal(MigrationStatus.ReadyWithConditions, b.Assessment.OverallStatus));
    }

    [Fact]
    public void SharedHostExe_IsOneLogicalDependency_AcrossAllThreeBatchBoundaries()
    {
        var report = BuildReport();

        var shared = Assert.Single(report.SharedInfrastructure, d => d.Type == MigrationDependencyType.SharedBinary);
        Assert.Equal(3, shared.AffectedBoundaryIds.Count);
        Assert.Contains("boundary:service:BatchA", shared.AffectedBoundaryIds);
        Assert.Contains("boundary:service:BatchB", shared.AffectedBoundaryIds);
        Assert.Contains("boundary:scheduledtask:\\ERP\\BatchC", shared.AffectedBoundaryIds);

        // Never duplicated per boundary: exactly one instance across the whole report.
        Assert.Single(report.Dependencies.Single(g => g.Type == MigrationDependencyType.SharedBinary).Dependencies);
    }

    [Fact]
    public void Dependencies_GroupedByAllFiveExpectedTypes()
    {
        var report = BuildReport();

        var types = report.Dependencies.Select(g => g.Type).OrderBy(t => t.ToString(), StringComparer.Ordinal).ToList();
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

    [Fact]
    public void Coverage_IsUnknown_WhenNoDiscoveryResultSupplied()
    {
        var report = BuildReport();

        Assert.Equal(AssessmentCoverage.Unknown, report.Coverage);
        Assert.Empty(report.CoverageWarnings);
    }
}
