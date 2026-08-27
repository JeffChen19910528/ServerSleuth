using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration;

/// <summary>
/// Shared-dependency migration impact — see skill.md (Phase 8A) §9: sharing affects impact/
/// scope, never severity. Reuses the same 3-boundary shared-`host.exe` scenario from
/// `SharedInfrastructureAttributionHardeningTests` (Phase 7B hardening), now run all the way
/// through <see cref="MigrationAssessmentEngine"/>.
/// </summary>
public class SharedDependencyMigrationImpactTests
{
    private static (ServerSleuth.Analysis.Migration.Models.MigrationAssessmentSummary Migration, RiskAggregationResult Aggregation) BuildSharedScenario()
    {
        var serviceA = EntityFactory.Service("MigA", @"D:\Mig\host.exe");
        var serviceB = EntityFactory.Service("MigB", @"D:\Mig\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Mig\MigC", @"D:\Mig\host.exe");
        var exe = EntityFactory.Dll(@"D:\Mig\host.exe");

        var entities = new List<DiscoveryEntity> { serviceA, serviceB, taskC, exe };
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var migration = new MigrationAssessmentEngine().Assess(context, result, aggregation);

        return (migration, aggregation);
    }

    [Fact]
    public void SharedFinding_ProducesOneMigrationIssuePerAffectedBoundary_AllConditional()
    {
        var (migration, _) = BuildSharedScenario();

        Assert.Equal(3, migration.Server.ApplicationAssessments.Count);
        foreach (var app in migration.Server.ApplicationAssessments)
        {
            var issue = Assert.Single(app.Issues, i => i.RuleId == "RR10-SharedInfrastructure");
            Assert.Equal(MigrationStatusImpact.Conditional, issue.MigrationStatusImpact);
            Assert.Equal(MigrationStatus.ReadyWithConditions, app.OverallStatus);
        }
    }

    [Fact]
    public void ServerLevel_CountsTheUnderlyingFindingOnce_NotThreeTimes()
    {
        var (migration, aggregation) = BuildSharedScenario();

        var sharedFindingIds = aggregation.Server.Findings.Where(f => f.Category == RiskCategory.SharedInfrastructure).Select(f => f.Id).Distinct().ToList();
        Assert.Single(sharedFindingIds);

        var serverIssue = Assert.Single(migration.Server.Issues, i => i.RuleId == "RR10-SharedInfrastructure");
        Assert.Equal(MigrationStatusImpact.Conditional, serverIssue.MigrationStatusImpact);
        Assert.Equal(3, serverIssue.AffectedBoundaryIds.Count);
    }

    [Fact]
    public void MigrationDependency_SharedBinary_ListsAllThreeAffectedBoundaries()
    {
        var (migration, _) = BuildSharedScenario();

        var dependency = Assert.Single(migration.Server.Dependencies, d => d.Type == MigrationDependencyType.SharedBinary);
        Assert.Equal(3, dependency.AffectedBoundaryIds.Count);
        Assert.NotNull(dependency.RelatedRiskFindingId);
    }

    [Fact]
    public void Sharing_NeverEscalatesServerStatus_BeyondWhatOtherFindingsWarrant()
    {
        // With ONLY the shared-infrastructure finding present, server status is
        // ReadyWithConditions — never Blocked/NeedsRemediation merely because 3 boundaries share it.
        var (migration, _) = BuildSharedScenario();

        Assert.Equal(MigrationStatus.ReadyWithConditions, migration.Server.OverallStatus);
        Assert.Equal(0, migration.Server.BlockingIssueCount);
        Assert.Equal(0, migration.Server.RemediationIssueCount);
    }
}
