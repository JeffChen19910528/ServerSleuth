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

/// <summary>
/// Determinism, no-mutation, and evidence-provenance guarantees — see skill.md (Phase 8B) §18-19,
/// §25.
/// </summary>
public class MigrationPlanEngineTests
{
    private static List<DiscoveryEntity> BuildScenario()
    {
        var service = EntityFactory.Service("DetSvc", @"D:\Det\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Det\svc.exe", notFound: true);
        var expiring = EntityFactory.Certificate("det.example.com", "DETCERT", validTo: DateTimeOffset.UtcNow.AddDays(10));
        var config = EntityFactory.Configuration(@"D:\Det\web.config");
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "DetDb");

        return [service, missingExe, expiring, config];
    }

    private static MigrationAssessmentSummary Assess(List<DiscoveryEntity> entities)
    {
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        return new MigrationAssessmentEngine().Assess(context, result, aggregation);
    }

    [Fact]
    public void Plan_IsDeterministic_AcrossRepeatedRuns()
    {
        var assessment = Assess(BuildScenario());

        var planA = MigrationPlanEngine.Plan(assessment);
        var planB = MigrationPlanEngine.Plan(assessment);

        Assert.Equal(planA.Actions.Select(a => a.ActionId), planB.Actions.Select(a => a.ActionId));
        Assert.Equal(planA.PreMigrationChecks.Select(c => c.CheckId), planB.PreMigrationChecks.Select(c => c.CheckId));
        Assert.Equal(planA.PostMigrationChecks.Select(c => c.CheckId), planB.PostMigrationChecks.Select(c => c.CheckId));
    }

    [Fact]
    public void Actions_AreSortedByActionId()
    {
        var plan = MigrationPlanEngine.Plan(Assess(BuildScenario()));

        var ids = plan.Actions.Select(a => a.ActionId).ToList();
        var sorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, ids);
    }

    [Fact]
    public void Checks_AreSortedByCheckId()
    {
        var plan = MigrationPlanEngine.Plan(Assess(BuildScenario()));

        Assert.Equal(plan.PreMigrationChecks.OrderBy(c => c.CheckId, StringComparer.Ordinal).Select(c => c.CheckId), plan.PreMigrationChecks.Select(c => c.CheckId));
        Assert.Equal(plan.PostMigrationChecks.OrderBy(c => c.CheckId, StringComparer.Ordinal).Select(c => c.CheckId), plan.PostMigrationChecks.Select(c => c.CheckId));
    }

    [Fact]
    public void Planning_NeverMutates_TheOriginalAssessment()
    {
        var assessment = Assess(BuildScenario());
        var issuesBefore = assessment.Server.Issues.Select(i => i.IssueId).ToList();
        var dependenciesBefore = assessment.Server.Dependencies.Select(d => d.DependencyId).ToList();

        MigrationPlanEngine.Plan(assessment);

        Assert.Equal(issuesBefore, assessment.Server.Issues.Select(i => i.IssueId));
        Assert.Equal(dependenciesBefore, assessment.Server.Dependencies.Select(d => d.DependencyId));
    }

    [Fact]
    public void EveryAction_TracesBackToARealIssue_WithNonEmptyEvidence()
    {
        var assessment = Assess(BuildScenario());
        var plan = MigrationPlanEngine.Plan(assessment);
        var issueIds = assessment.Server.Issues.Select(i => i.IssueId).ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(plan.Actions);
        foreach (var action in plan.Actions)
        {
            var relatedIssueId = Assert.Single(action.RelatedIssueIds);
            Assert.Contains(relatedIssueId, issueIds);
            Assert.NotEmpty(action.Evidence);
        }
    }

    [Fact]
    public void EveryCheck_TracesBackToARealActionOrDependency()
    {
        var assessment = Assess(BuildScenario());
        var plan = MigrationPlanEngine.Plan(assessment);
        var actionIds = plan.Actions.Select(a => a.ActionId).ToHashSet(StringComparer.Ordinal);
        var dependencyIds = plan.Dependencies.Select(d => d.DependencyId).ToHashSet(StringComparer.Ordinal);

        foreach (var check in plan.PreMigrationChecks.Concat(plan.PostMigrationChecks))
        {
            Assert.True(
                check.RelatedActionIds.All(actionIds.Contains) && check.RelatedDependencyIds.All(dependencyIds.Contains),
                $"Check {check.CheckId} references an action/dependency that doesn't exist in this plan.");
            Assert.True(check.RelatedActionIds.Count > 0 || check.RelatedDependencyIds.Count > 0,
                $"Check {check.CheckId} has no provenance at all.");
        }
    }

    [Fact]
    public void ReviewGraphIntegrityAction_ProducesNoVerificationChecks()
    {
        var entities = BuildScenario();
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var assessment = new MigrationAssessmentEngine().Assess(context, result, aggregation);

        // No RR12-GraphIntegrity finding is producible from this simple scenario; assert the
        // planner's own contract holds structurally instead — a synthetic action of that type
        // must never appear in either check list once fed through the verification planner.
        var plan = MigrationPlanEngine.Plan(assessment);
        Assert.DoesNotContain(plan.Actions, a => a.ActionType == MigrationActionType.ReviewGraphIntegrity);

        var syntheticAction = new MigrationAction
        {
            ActionId = "action:synthetic-graph-integrity",
            ActionType = MigrationActionType.ReviewGraphIntegrity,
            Title = "Synthetic",
            Description = "Synthetic",
            Priority = MigrationActionPriority.Critical,
            Phase = MigrationVerificationPhase.PreMigration,
            AffectedBoundaryIds = [],
            AffectedEntityIds = [],
            RelatedIssueIds = ["migration:synthetic"],
            RelatedDependencyIds = [],
            Evidence = [],
            Rationale = "Synthetic test action"
        };

        var (pre, post, _) = MigrationVerificationPlanner.Plan(assessment.Server, [syntheticAction]);
        Assert.DoesNotContain(pre, c => c.RelatedActionIds.Contains(syntheticAction.ActionId));
        Assert.DoesNotContain(post, c => c.RelatedActionIds.Contains(syntheticAction.ActionId));
    }
}
