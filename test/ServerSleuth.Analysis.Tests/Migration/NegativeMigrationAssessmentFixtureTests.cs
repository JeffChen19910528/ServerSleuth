using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Diagnostics;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration;

/// <summary>Negative fixtures, skill.md (Phase 8A) §18.</summary>
public class NegativeMigrationAssessmentFixtureTests
{
    private static MigrationAssessmentSummary RunAndAssess(List<DiscoveryEntity> entities)
    {
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        return new MigrationAssessmentEngine().Assess(context, result, aggregation);
    }

    // 1. No findings -> Ready.
    [Fact]
    public void NoFindings_IsReady()
    {
        var migration = RunAndAssess([]);
        Assert.Equal(MigrationStatus.Ready, migration.Server.OverallStatus);
        Assert.Empty(migration.Server.Issues);
    }

    // 2. Info-only -> Ready. Achieved via the real ConfigurationRiskRule's EnvVar marker
    // (always Info severity — see ConfigurationRiskRule.Markers).
    [Fact]
    public void InfoOnlyFindings_IsReady()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config", dependencyReferences: ["EnvVar: APP_HOME"]);
        var migration = RunAndAssess([config]);

        Assert.NotEmpty(migration.Server.Issues);
        Assert.All(migration.Server.Issues, i => Assert.Equal(MigrationStatusImpact.Informational, i.MigrationStatusImpact));
        Assert.Equal(MigrationStatus.Ready, migration.Server.OverallStatus);
    }

    // 3. Low-only -> Ready. RR11-ConfigurationRisk's UnixSocket marker is always Low.
    [Fact]
    public void LowOnlyFindings_IsReady()
    {
        var config = EntityFactory.Configuration("/etc/app/app.conf", dependencyReferences: ["UnixSocket: /var/run/app.sock"]);
        var migration = RunAndAssess([config]);

        Assert.NotEmpty(migration.Server.Issues);
        Assert.All(migration.Server.Issues, i => Assert.Equal(RiskSeverity.Low, i.Severity));
        Assert.Equal(MigrationStatus.Ready, migration.Server.OverallStatus);
    }

    // 4. Medium external dependency -> ReadyWithConditions.
    [Fact]
    public void MediumExternalDependency_IsReadyWithConditions()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config");
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "AppDb");

        var migration = RunAndAssess([config]);

        var issue = Assert.Single(migration.Server.Issues, i => i.RuleId == "RR9-ExternalDependency");
        Assert.Equal(RiskSeverity.Medium, issue.Severity);
        Assert.Equal(MigrationStatusImpact.Conditional, issue.MigrationStatusImpact);
        Assert.Equal(MigrationStatus.ReadyWithConditions, migration.Server.OverallStatus);
    }

    // 5. High certificate issue -> NeedsRemediation.
    [Fact]
    public void HighCertificateIssue_IsNeedsRemediation()
    {
        var expiring = EntityFactory.Certificate("neg5.example.com", "NEGCERT5", validTo: DateTimeOffset.UtcNow.AddDays(10));
        var migration = RunAndAssess([expiring]);

        var issue = Assert.Single(migration.Server.Issues, i => i.RuleId == "RR5-CertificateExpiry");
        Assert.Equal(RiskSeverity.High, issue.Severity);
        Assert.Equal(MigrationStatus.NeedsRemediation, migration.Server.OverallStatus);
    }

    // 6. Critical missing required executable -> Blocked (Service's own executable).
    [Fact]
    public void CriticalMissingServiceExecutable_IsBlocked()
    {
        var service = EntityFactory.Service("Neg6Svc", @"D:\Neg6\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Neg6\svc.exe", notFound: true);
        var migration = RunAndAssess([service, missingExe]);

        Assert.Equal(MigrationStatus.Blocked, migration.Server.OverallStatus);
        Assert.Equal(1, migration.Server.BlockingIssueCount);
    }

    // 7. Critical missing native dependency -> Blocked. NOTE: with the current rule set, a
    // Critical severity is only reachable via MissingBinaryRule (RR2)/ServiceDependencyRule
    // (RR6) when the dependent is itself a Service/ScheduledTask — RR1-MissingDependency
    // (unresolved import table entries with no discovered binary at all) is always High, never
    // Critical (see MissingDependencyRule.DefaultSeverity). This scenario is therefore built via
    // the same RR2-Critical policy path as scenario 6 above (a distinct ScheduledTask this time,
    // not a Service), matching skill.md's own "if actual rule outputs differ, preserve the
    // actual evidence-backed result and document why."
    [Fact]
    public void CriticalMissingScheduledTaskExecutable_IsBlocked_SameRuleFamilyAsScenario6()
    {
        var task = EntityFactory.ScheduledTask(@"\Neg7\Task", @"D:\Neg7\task.exe");
        var missingExe = EntityFactory.Dll(@"D:\Neg7\task.exe", notFound: true);
        var migration = RunAndAssess([task, missingExe]);

        Assert.Equal(MigrationStatus.Blocked, migration.Server.OverallStatus);
        Assert.Equal(1, migration.Server.BlockingIssueCount);
        Assert.Equal("RR2-MissingBinary", migration.Server.Issues.Single(i => i.MigrationStatusImpact == MigrationStatusImpact.Blocking).RuleId);
    }

    // 8. Shared dependency across 3 boundaries -> affects all 3, severity/impact unchanged.
    [Fact]
    public void SharedDependencyAcrossThreeBoundaries_AffectsAllThree_NeverEscalatesSeverity()
    {
        var serviceA = EntityFactory.Service("Neg8A", @"D:\Neg8\host.exe");
        var serviceB = EntityFactory.Service("Neg8B", @"D:\Neg8\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Neg8\Neg8C", @"D:\Neg8\host.exe");
        var exe = EntityFactory.Dll(@"D:\Neg8\host.exe");

        var migration = RunAndAssess([serviceA, serviceB, taskC, exe]);

        Assert.Equal(3, migration.Server.ApplicationAssessments.Count);
        Assert.All(migration.Server.ApplicationAssessments, a =>
        {
            var issue = Assert.Single(a.Issues);
            Assert.Equal(RiskSeverity.Medium, issue.Severity);
            Assert.Equal(MigrationStatusImpact.Conditional, issue.MigrationStatusImpact);
        });
    }

    // 9. Expected orphan runtime -> no migration blocker.
    [Fact]
    public void ExpectedOrphanRuntime_NoMigrationBlocker()
    {
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var migration = RunAndAssess([runtime]);

        Assert.Equal(MigrationStatus.Ready, migration.Server.OverallStatus);
        Assert.Empty(migration.Server.Issues);
    }

    // 10. Expected orphan certificate -> no migration blocker.
    [Fact]
    public void ExpectedOrphanCertificate_NoMigrationBlocker()
    {
        var certificate = EntityFactory.Certificate("neg10.example.com", "NEGORPHAN10", validTo: DateTimeOffset.UtcNow.AddYears(2));
        var migration = RunAndAssess([certificate]);

        Assert.Equal(MigrationStatus.Ready, migration.Server.OverallStatus);
        Assert.Empty(migration.Server.Issues);
    }

    // 11. Same-named binaries at different paths -> no false dependency.
    [Fact]
    public void SameNamedBinariesAtDifferentPaths_NoFalseSharedDependency()
    {
        var serviceA = EntityFactory.Service("Neg11A", @"D:\Neg11A\bin\Common.exe");
        var exeA = EntityFactory.Dll(@"D:\Neg11A\bin\Common.exe");
        var serviceB = EntityFactory.Service("Neg11B", @"D:\Neg11B\bin\Common.exe");
        var exeB = EntityFactory.Dll(@"D:\Neg11B\bin\Common.exe");

        var migration = RunAndAssess([serviceA, exeA, serviceB, exeB]);

        Assert.DoesNotContain(migration.Server.Dependencies, d => d.Type == MigrationDependencyType.SharedBinary);
        Assert.Equal(MigrationStatus.Ready, migration.Server.OverallStatus);
    }

    // 12. Server-scoped risk -> remains visible at server level.
    [Fact]
    public void ServerScopedRisk_RemainsVisibleAtServerLevel()
    {
        var expiring = EntityFactory.Certificate("neg12.example.com", "NEGCERT12", validTo: DateTimeOffset.UtcNow.AddDays(3));
        var migration = RunAndAssess([expiring]);

        Assert.Empty(migration.Server.ApplicationAssessments);
        Assert.NotEmpty(migration.Server.Issues);
        Assert.Contains(migration.Server.Issues, i => i.RuleId == "RR5-CertificateExpiry");
    }
}
