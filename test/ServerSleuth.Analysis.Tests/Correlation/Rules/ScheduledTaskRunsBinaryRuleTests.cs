using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;

namespace ServerSleuth.Analysis.Tests.Correlation.Rules;

public class ScheduledTaskRunsBinaryRuleTests
{
    [Fact]
    public void Evaluate_TaskActionMatchesBinary_ProducesRunsCandidate()
    {
        var dll = EntityFactory.Dll(@"D:\ERP\ERPWorker.exe");
        var task = EntityFactory.ScheduledTask(@"\ERP\Nightly", @"D:\ERP\ERPWorker.exe");
        var context = new CorrelationContext([dll, task]);

        var candidates = new ScheduledTaskRunsBinaryRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Equal(task.Id, candidate.SourceEntityId);
        Assert.Equal(dll.Id, candidate.TargetEntityId);
        Assert.Equal(DependencyEdgeType.Runs, candidate.Type);
    }

    [Fact]
    public void Evaluate_MissingBinary_ProducesUnresolvedCandidate()
    {
        var task = EntityFactory.ScheduledTask(@"\ERP\Nightly", @"D:\ERP\Missing.exe");
        var context = new CorrelationContext([task]);

        var candidates = new ScheduledTaskRunsBinaryRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Null(candidate.TargetEntityId);
    }

    [Fact]
    public void Evaluate_NoAction_ProducesNoCandidate()
    {
        var task = EntityFactory.ScheduledTask(@"\ERP\Nightly", null);
        var context = new CorrelationContext([task]);

        var candidates = new ScheduledTaskRunsBinaryRule().Evaluate(context);

        Assert.Empty(candidates);
    }
}
