using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration.Planning;

/// <summary>
/// Runs the exact same 17-entity ERP fixture as <c>ErpMigrationAssessmentFixtureTests</c> (Phase
/// 8A) all the way through <see cref="MigrationPlanEngine"/> — see skill.md (Phase 8B) §21.
///
/// Known-good baseline from the Phase 8A fixture: Server has 1 Blocking + 4 RemediationRequired +
/// 3 Conditional = 8 non-Informational issues (RR2/RR3/RR4/RR5/RR6/RR9x2/RR10, every one of which
/// this planner has an ActionType mapping for) and exactly 5 Dependencies (Certificate, Database,
/// FileShare, Runtime, SharedBinary), each carrying a <c>RelatedRiskFindingId</c> that traces back
/// to one of those 8 issues — so every dependency is expected to fold into an existing action
/// rather than produce an orphan check.
/// </summary>
public class ErpMigrationPlanFixtureTests
{
    private static MigrationPlan BuildPlan()
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
        return MigrationPlanEngine.Plan(assessment);
    }

    [Fact]
    public void EveryNonInformationalIssue_ProducesExactlyOneAction()
    {
        var plan = BuildPlan();

        Assert.Equal(8, plan.Assessment.Server.Issues.Count);
        Assert.Equal(8, plan.Actions.Count);
        Assert.Equal(8, plan.Diagnostics.Actions.ActionsCreated);
        Assert.Equal(0, plan.Diagnostics.Actions.SkippedUnclassifiedIssues);
        Assert.Equal(0, plan.Diagnostics.Actions.SkippedInformationalIssues);
    }

    [Fact]
    public void EveryDependency_FoldsIntoAnExistingAction_NoOrphanChecks()
    {
        var plan = BuildPlan();

        Assert.Equal(5, plan.Dependencies.Count);
        Assert.Equal(5, plan.Actions.Sum(a => a.RelatedDependencyIds.Count));
        Assert.Equal(0, plan.Diagnostics.Verification.OrphanDependencyChecksCreated);
    }

    [Fact]
    public void ErpWorker_MissingExecutable_ProducesCriticalPrepareMissingBinaryAction()
    {
        var plan = BuildPlan();

        var action = Assert.Single(plan.Actions, a => a.AffectedBoundaryIds.Contains("boundary:service:ERPWorker"));
        Assert.Equal(MigrationActionType.PrepareMissingBinary, action.ActionType);
        Assert.Equal(MigrationActionPriority.Critical, action.Priority);
        Assert.Equal(MigrationVerificationPhase.PreMigration, action.Phase);
    }

    [Fact]
    public void SharedHostExe_ProducesOneLogicalAction_AffectingAllThreeBatchBoundaries()
    {
        var plan = BuildPlan();

        var action = Assert.Single(plan.Actions, a => a.ActionType == MigrationActionType.DocumentDependency);
        Assert.Equal(3, action.AffectedBoundaryIds.Count);
        Assert.Contains("boundary:service:BatchA", action.AffectedBoundaryIds);
        Assert.Contains("boundary:service:BatchB", action.AffectedBoundaryIds);
        Assert.Contains("boundary:scheduledtask:\\ERP\\BatchC", action.AffectedBoundaryIds);
        Assert.Single(action.RelatedDependencyIds);
    }

    [Fact]
    public void CertificateAction_HasMatchingPreAndPostVerificationChecks()
    {
        var plan = BuildPlan();

        var action = Assert.Single(plan.Actions, a => a.ActionType == MigrationActionType.PrepareCertificate);

        var preCheck = Assert.Single(plan.PreMigrationChecks, c => c.RelatedActionIds.Contains(action.ActionId));
        Assert.Equal(MigrationVerificationPhase.PreMigration, preCheck.Phase);
        Assert.Equal(MigrationActionType.VerifyCertificate, preCheck.CheckType);

        var postCheck = Assert.Single(plan.PostMigrationChecks, c => c.RelatedActionIds.Contains(action.ActionId));
        Assert.Equal(MigrationVerificationPhase.PostMigration, postCheck.Phase);
        Assert.Equal(MigrationActionType.VerifyCertificate, postCheck.CheckType);
        Assert.Equal(action.RelatedDependencyIds, postCheck.RelatedDependencyIds);
    }

    [Fact]
    public void Plan_NeverInventsExecutableCommands()
    {
        var plan = BuildPlan();

        foreach (var action in plan.Actions)
        {
            Assert.DoesNotContain("powershell", action.Description, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("systemctl", action.Description, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("docker ", action.Description, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("kubectl", action.Description, StringComparison.OrdinalIgnoreCase);
        }
    }
}
