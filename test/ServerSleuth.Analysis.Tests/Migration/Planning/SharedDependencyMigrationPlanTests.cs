using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration.Planning;

/// <summary>
/// Shared-infrastructure action/check semantics — see skill.md (Phase 8B) §7, §17, §20: a binary
/// shared by several boundaries must produce exactly one logical <see cref="MigrationAction"/>
/// (never one per boundary) with every affected boundary listed on that single action, and a
/// same-named binary at a different path must remain entirely separate.
/// </summary>
public class SharedDependencyMigrationPlanTests
{
    private static MigrationPlan Build(List<DiscoveryEntity> entities)
    {
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var assessment = new MigrationAssessmentEngine().Assess(context, result, aggregation);
        return MigrationPlanEngine.Plan(assessment);
    }

    [Fact]
    public void SharedHealthyBinary_AcrossThreeBoundaries_ProducesOneAction_NotThree()
    {
        var serviceA = EntityFactory.Service("PlanA", @"D:\Plan\host.exe");
        var serviceB = EntityFactory.Service("PlanB", @"D:\Plan\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Plan\PlanC", @"D:\Plan\host.exe");
        var exe = EntityFactory.Dll(@"D:\Plan\host.exe");

        var plan = Build([serviceA, serviceB, taskC, exe]);

        var sharedActions = plan.Actions.Where(a => a.ActionType == MigrationActionType.DocumentDependency).ToList();
        var action = Assert.Single(sharedActions);
        Assert.Equal(3, action.AffectedBoundaryIds.Count);

        var dependency = Assert.Single(plan.Dependencies, d => d.Type == MigrationDependencyType.SharedBinary);
        Assert.Contains(dependency.DependencyId, action.RelatedDependencyIds);
    }

    [Fact]
    public void SameNamedBinariesAtDifferentPaths_NeverMergedIntoOneAction()
    {
        var serviceA = EntityFactory.Service("DiffA", @"D:\DiffA\bin\Common.exe");
        var exeA = EntityFactory.Dll(@"D:\DiffA\bin\Common.exe");
        var serviceB = EntityFactory.Service("DiffB", @"D:\DiffB\bin\Common.exe");
        var exeB = EntityFactory.Dll(@"D:\DiffB\bin\Common.exe");

        var plan = Build([serviceA, exeA, serviceB, exeB]);

        Assert.DoesNotContain(plan.Actions, a => a.ActionType == MigrationActionType.DocumentDependency);
        Assert.DoesNotContain(plan.Dependencies, d => d.Type == MigrationDependencyType.SharedBinary);
    }

    [Fact]
    public void MissingSharedBinary_AcrossServiceAndScheduledTask_MergedByPhase7A_YieldsOneCriticalAction()
    {
        // MissingBinaryEntityId merge anchor (Phase 7A) already collapses RR2/RR6/RR7 findings
        // about the literal same missing file into one RiskFinding before this planner runs —
        // this test proves that collapse survives all the way through to one MigrationAction.
        var service = EntityFactory.Service("MergeSvc", @"D:\Merge\shared.exe");
        var task = EntityFactory.ScheduledTask(@"\Merge\Task", @"D:\Merge\shared.exe");
        var missingExe = EntityFactory.Dll(@"D:\Merge\shared.exe", notFound: true);

        var plan = Build([service, task, missingExe]);

        var action = Assert.Single(plan.Actions, a => a.ActionType == MigrationActionType.PrepareMissingBinary);
        Assert.Equal(MigrationActionPriority.Critical, action.Priority);
        Assert.Single(action.RelatedIssueIds);
    }
}
