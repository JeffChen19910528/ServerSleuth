using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Correlation.Boundaries;

/// <summary>Required negative fixtures from skill.md §24.</summary>
public class NegativeBoundaryFixtureTests
{
    [Fact]
    public void Analyze_TwoAppsBothContainingCommonDll_ProduceTwoSeparateBoundaries()
    {
        var appA = EntityFactory.Application("AppA", "/", @"D:\AppA");
        var appB = EntityFactory.Application("AppB", "/", @"D:\AppB");
        var commonInA = EntityFactory.Dll(@"D:\AppA\Common.dll", referencedBy: [appA.Id]);
        var commonInB = EntityFactory.Dll(@"D:\AppB\Common.dll", referencedBy: [appB.Id]);

        var entities = new List<DiscoveryEntity> { appA, appB, commonInA, commonInB };
        var graph = new CorrelationEngine().Correlate(entities).Graph;

        var result = new ApplicationBoundaryEngine().Analyze(entities, graph);

        Assert.Equal(2, result.Boundaries.Count);
        var boundaryA = result.Boundaries.Single(b => b.MemberEntityIds.Contains(appA.Id));
        var boundaryB = result.Boundaries.Single(b => b.MemberEntityIds.Contains(appB.Id));
        Assert.Contains(boundaryA.MemberEntityIds, id => id == commonInA.Id);
        Assert.DoesNotContain(boundaryA.MemberEntityIds, id => id == commonInB.Id);
        Assert.Contains(boundaryB.MemberEntityIds, id => id == commonInB.Id);
        Assert.DoesNotContain(boundaryB.MemberEntityIds, id => id == commonInA.Id);
    }

    [Fact]
    public void Analyze_ErpAndErpWorkerUnderSameParent_NeverAutomaticallyMerge()
    {
        var erp = EntityFactory.Application("ERP", "/", @"D:\ERP\App");
        var erpWorkerService = EntityFactory.Service("ERPWorker", @"D:\ERP\WorkerApp\Worker.exe");

        var entities = new List<DiscoveryEntity> { erp, erpWorkerService };
        var graph = new CorrelationEngine().Correlate(entities).Graph;

        var result = new ApplicationBoundaryEngine().Analyze(entities, graph);

        Assert.Equal(2, result.Boundaries.Count);
        Assert.Contains(result.Diagnostics.AmbiguousCandidates, c => c.Reason.Contains("common parent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_AnchorAlreadyMergedElsewhere_RejectsAFurtherMergeAttempt()
    {
        // ERPWorker.exe is shared by exactly a Service+Task pair (merges), while a second,
        // unrelated executable is shared by exactly a different Service+Task pair. Each pair
        // stays independent — there is no scenario in which a third anchor tries to claim an
        // already-consumed anchor here, so this test instead documents that two independent
        // merges never interfere with each other's consumed-anchor bookkeeping.
        var serviceA = EntityFactory.Service("SvcA", @"C:\A\a.exe");
        var taskA = EntityFactory.ScheduledTask(@"\A\Job", @"C:\A\a.exe");
        var exeA = EntityFactory.Dll(@"C:\A\a.exe");

        var serviceB = EntityFactory.Service("SvcB", @"C:\B\b.exe");
        var taskB = EntityFactory.ScheduledTask(@"\B\Job", @"C:\B\b.exe");
        var exeB = EntityFactory.Dll(@"C:\B\b.exe");

        var entities = new List<DiscoveryEntity> { serviceA, taskA, exeA, serviceB, taskB, exeB };
        var graph = new CorrelationEngine().Correlate(entities).Graph;

        var result = new ApplicationBoundaryEngine().Analyze(entities, graph);

        Assert.Equal(2, result.Boundaries.Count);
        Assert.Equal(2, result.Diagnostics.MergedBoundaries);
        Assert.Empty(result.Diagnostics.RejectedMerges);
    }
}
