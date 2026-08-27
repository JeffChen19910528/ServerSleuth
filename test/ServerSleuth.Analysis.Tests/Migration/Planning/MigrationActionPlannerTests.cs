using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration.Planning;

/// <summary>Direct unit coverage of <see cref="MigrationActionPlanner"/> — see skill.md
/// (Phase 8B) §6, §16, §17.</summary>
public class MigrationActionPlannerTests
{
    private static (IReadOnlyList<MigrationAction> Actions, MigrationActionDiagnostics Diagnostics) PlanFrom(List<DiscoveryEntity> entities)
    {
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var assessment = new MigrationAssessmentEngine().Assess(context, result, aggregation);
        return MigrationActionPlanner.Plan(assessment.Server);
    }

    [Fact]
    public void AccessDeniedIssue_ProducesReviewAccessDeniedAction_MediumPriority()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config");
        config.SetMetadata("ParseStatus", "AccessDenied");

        var (actions, _) = PlanFrom([config]);

        var action = Assert.Single(actions);
        Assert.Equal(MigrationActionType.ReviewAccessDenied, action.ActionType);
        Assert.Equal(MigrationActionPriority.Medium, action.Priority);
    }

    [Fact]
    public void MissingRuntimeIssue_ProducesPrepareRuntimeAction_HighPriority()
    {
        var app = EntityFactory.Application("RuntimeApp", "/", @"D:\RuntimeApp");
        var config = EntityFactory.Configuration(@"D:\RuntimeApp\web.config", ownerEntityId: app.Id, dependencyReferences: ["RuntimeVersion: net8.0"]);

        var (actions, diagnostics) = PlanFrom([app, config]);

        var runtimeAction = Assert.Single(actions, a => a.ActionType == MigrationActionType.PrepareRuntime);
        Assert.Equal(MigrationActionPriority.High, runtimeAction.Priority);
        Assert.True(diagnostics.ActionsCreated >= 1);
    }

    [Fact]
    public void ConfigurationFileShareRisk_ProducesPrepareConfigurationAction()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config", dependencyReferences: [@"FileShare: \\FILESERVER\Share"]);

        var (actions, _) = PlanFrom([config]);

        Assert.Contains(actions, a => a.ActionType == MigrationActionType.PrepareConfiguration);
    }

    [Fact]
    public void Diagnostics_CountEveryIssueConsidered_EvenWhenSkipped()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config", dependencyReferences: ["EnvVar: APP_HOME"]);

        var (actions, diagnostics) = PlanFrom([config]);

        Assert.Empty(actions);
        Assert.True(diagnostics.IssuesConsidered > 0);
        Assert.Equal(diagnostics.IssuesConsidered, diagnostics.SkippedInformationalIssues);
    }
}
