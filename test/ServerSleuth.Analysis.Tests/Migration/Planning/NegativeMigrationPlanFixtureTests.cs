using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Migration.Verification;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration.Planning;

/// <summary>Negative fixtures for Migration Planning — see skill.md (Phase 8B) §22.</summary>
public class NegativeMigrationPlanFixtureTests
{
    private static MigrationPlan Build(List<DiscoveryEntity> entities)
    {
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var assessment = new MigrationAssessmentEngine().Assess(context, result, aggregation);
        return MigrationPlanEngine.Plan(assessment);
    }

    // 1. No issues -> no actions at all.
    [Fact]
    public void NoIssues_NoActions()
    {
        var plan = Build([]);

        Assert.Empty(plan.Actions);
        Assert.Empty(plan.PreMigrationChecks);
        Assert.Empty(plan.PostMigrationChecks);
    }

    // 2. Info-only -> informational/inventory checks only, never a remediation action.
    [Fact]
    public void InfoOnlyFindings_InformationalChecksOnly_NoActions()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config", dependencyReferences: ["EnvVar: APP_HOME"]);
        var plan = Build([config]);

        Assert.Empty(plan.Actions);
        Assert.Empty(plan.PreMigrationChecks);
        Assert.NotEmpty(plan.PostMigrationChecks);
        Assert.All(plan.PostMigrationChecks, c => Assert.Empty(c.RelatedActionIds));
        Assert.True(plan.Diagnostics.Verification.InformationalChecksCreated > 0);
    }

    // 3. Low-only -> no blocking action; RR11's UnixSocket marker is Low severity but still
    // Informational impact (see MigrationPolicy), so still no action.
    [Fact]
    public void LowOnlyFindings_NoAction()
    {
        var config = EntityFactory.Configuration("/etc/app/app.conf", dependencyReferences: ["UnixSocket: /var/run/app.sock"]);
        var plan = Build([config]);

        Assert.Empty(plan.Actions);
    }

    // 4. Medium external dependency -> a VerifyExternalDependency action + matching checks.
    [Fact]
    public void MediumExternalDependency_ProducesVerifyExternalDependencyAction()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config");
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "AppDb");

        var plan = Build([config]);

        var action = Assert.Single(plan.Actions);
        Assert.Equal(MigrationActionType.VerifyExternalDependency, action.ActionType);
        Assert.Equal(MigrationActionPriority.Medium, action.Priority);
        Assert.Single(plan.PreMigrationChecks);
        Assert.Single(plan.PostMigrationChecks);
    }

    // 5. High certificate issue -> PrepareCertificate action + certificate verification checks.
    [Fact]
    public void HighCertificateIssue_ProducesPrepareCertificateAction()
    {
        var expiring = EntityFactory.Certificate("neg5.example.com", "NEGCERT5", validTo: DateTimeOffset.UtcNow.AddDays(10));
        var plan = Build([expiring]);

        var action = Assert.Single(plan.Actions);
        Assert.Equal(MigrationActionType.PrepareCertificate, action.ActionType);
        Assert.Equal(MigrationActionPriority.High, action.Priority);

        var postCheck = Assert.Single(plan.PostMigrationChecks);
        Assert.Equal(MigrationActionType.VerifyCertificate, postCheck.CheckType);
    }

    // 6. Critical missing executable -> critical remediation action.
    [Fact]
    public void CriticalMissingServiceExecutable_ProducesCriticalAction()
    {
        var service = EntityFactory.Service("Neg6Svc", @"D:\Neg6\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Neg6\svc.exe", notFound: true);
        var plan = Build([service, missingExe]);

        var action = Assert.Single(plan.Actions);
        Assert.Equal(MigrationActionType.PrepareMissingBinary, action.ActionType);
        Assert.Equal(MigrationActionPriority.Critical, action.Priority);
    }

    // 7. Critical native dependency (ScheduledTask flavor of the same RR2 policy path) -> critical action.
    [Fact]
    public void CriticalMissingScheduledTaskExecutable_ProducesCriticalAction()
    {
        var task = EntityFactory.ScheduledTask(@"\Neg7\Task", @"D:\Neg7\task.exe");
        var missingExe = EntityFactory.Dll(@"D:\Neg7\task.exe", notFound: true);
        var plan = Build([task, missingExe]);

        var action = Assert.Single(plan.Actions);
        Assert.Equal(MigrationActionType.PrepareMissingBinary, action.ActionType);
        Assert.Equal(MigrationActionPriority.Critical, action.Priority);
    }

    // 8. Shared dependency across 3 boundaries -> one logical action, 3 affected boundaries.
    [Fact]
    public void SharedDependencyAcrossThreeBoundaries_OneAction_ThreeBoundaries()
    {
        var serviceA = EntityFactory.Service("Neg8A", @"D:\Neg8\host.exe");
        var serviceB = EntityFactory.Service("Neg8B", @"D:\Neg8\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Neg8\Neg8C", @"D:\Neg8\host.exe");
        var exe = EntityFactory.Dll(@"D:\Neg8\host.exe");

        var plan = Build([serviceA, serviceB, taskC, exe]);

        var action = Assert.Single(plan.Actions);
        Assert.Equal(MigrationActionType.DocumentDependency, action.ActionType);
        Assert.Equal(3, action.AffectedBoundaryIds.Count);
    }

    // 9. Same-named different-path binaries -> separate dependencies/actions, no false merge.
    [Fact]
    public void SameNamedDifferentPathBinaries_RemainSeparate()
    {
        var serviceA = EntityFactory.Service("Neg9A", @"D:\Neg9A\bin\Common.exe");
        var exeA = EntityFactory.Dll(@"D:\Neg9A\bin\Common.exe");
        var serviceB = EntityFactory.Service("Neg9B", @"D:\Neg9B\bin\Common.exe");
        var exeB = EntityFactory.Dll(@"D:\Neg9B\bin\Common.exe");

        var plan = Build([serviceA, exeA, serviceB, exeB]);

        Assert.Empty(plan.Actions);
        Assert.DoesNotContain(plan.Dependencies, d => d.Type == MigrationDependencyType.SharedBinary);
    }

    // 10. Expected orphan runtime -> no false remediation action.
    [Fact]
    public void ExpectedOrphanRuntime_NoFalseAction()
    {
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var plan = Build([runtime]);

        Assert.Empty(plan.Actions);
        Assert.Empty(plan.PreMigrationChecks);
        Assert.Empty(plan.PostMigrationChecks);
    }

    // 11. Expected orphan certificate -> no false remediation action.
    [Fact]
    public void ExpectedOrphanCertificate_NoFalseAction()
    {
        var certificate = EntityFactory.Certificate("neg11.example.com", "NEGORPHAN11", validTo: DateTimeOffset.UtcNow.AddYears(2));
        var plan = Build([certificate]);

        Assert.Empty(plan.Actions);
        Assert.Empty(plan.PreMigrationChecks);
        Assert.Empty(plan.PostMigrationChecks);
    }

    // 12. Server-scoped risk -> server-level action/check remains visible even with no
    // ApplicationBoundary attribution.
    [Fact]
    public void ServerScopedRisk_ActionAndCheckRemainVisible()
    {
        var expiring = EntityFactory.Certificate("neg12.example.com", "NEGCERT12", validTo: DateTimeOffset.UtcNow.AddDays(3));
        var plan = Build([expiring]);

        Assert.Empty(plan.Assessment.Server.ApplicationAssessments);
        var action = Assert.Single(plan.Actions);
        Assert.Empty(action.AffectedBoundaryIds);
        Assert.NotEmpty(plan.PreMigrationChecks);
        Assert.NotEmpty(plan.PostMigrationChecks);
    }
}
