using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

public class ScheduledTaskDependencyRuleTests
{
    private static readonly IReadOnlyList<Analysis.Risk.Rules.IRiskRule> OnlyThisRule = [new ScheduledTaskDependencyRule()];

    [Fact]
    public void TaskActionNeverResolved_ProducesHighFinding()
    {
        var task = EntityFactory.ScheduledTask(@"\ERP\NightlyImport", @"D:\ERP\Jobs\NightlyImport.exe");
        var entities = new List<DiscoveryEntity> { task };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskCategory.ScheduledTask, finding.Category);
        Assert.Equal(RiskSeverity.High, finding.Severity);
        Assert.Equal(task.Id, finding.SourceEntityId);
    }

    [Fact]
    public void TaskActionMissingOnDisk_ProducesHighFinding_WithMergeAnchor()
    {
        var task = EntityFactory.ScheduledTask(@"\ERP\NightlyImport", @"D:\ERP\Jobs\NightlyImport.exe");
        var dll = EntityFactory.Dll(@"D:\ERP\Jobs\NightlyImport.exe", notFound: true);
        var entities = new List<DiscoveryEntity> { task, dll };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.High, finding.Severity);
        Assert.Contains(dll.Id, finding.RelatedEntityIds);
        Assert.Equal(dll.Id, finding.Metadata["MissingBinaryEntityId"]);
    }

    [Fact]
    public void TaskActionFoundOnDisk_NeverProducesFinding()
    {
        var task = EntityFactory.ScheduledTask(@"\ERP\NightlyImport", @"D:\ERP\Jobs\NightlyImport.exe");
        var dll = EntityFactory.Dll(@"D:\ERP\Jobs\NightlyImport.exe", notFound: false);
        var entities = new List<DiscoveryEntity> { task, dll };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void TaskActionNotAnExplicitAbsolutePath_NeverGuessedAt_NeverProducesFinding()
    {
        var task = EntityFactory.ScheduledTask(@"\ERP\RelativeAction", "some-command-name");
        var entities = new List<DiscoveryEntity> { task };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void TaskWithNoAction_NeverProducesFinding()
    {
        var task = EntityFactory.ScheduledTask(@"\ERP\NoAction", null);
        var entities = new List<DiscoveryEntity> { task };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }
}
