using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

public class SharedInfrastructureRuleTests
{
    private static readonly IReadOnlyList<Analysis.Risk.Rules.IRiskRule> OnlyThisRule = [new SharedInfrastructureRule()];

    [Fact]
    public void ThreeWorkloadsShareOneExecutable_ProducesMediumFinding_ReferencingAllThreeSharers()
    {
        var serviceA = EntityFactory.Service("SvcA", @"C:\Shared\host.exe");
        var serviceB = EntityFactory.Service("SvcB", @"C:\Shared\host.exe");
        var task = EntityFactory.ScheduledTask(@"\Shared\Job", @"C:\Shared\host.exe");
        var hostExe = EntityFactory.Dll(@"C:\Shared\host.exe");

        var entities = new List<DiscoveryEntity> { serviceA, serviceB, task, hostExe };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskCategory.SharedInfrastructure, finding.Category);
        Assert.Equal(RiskSeverity.Medium, finding.Severity);
        Assert.Equal(hostExe.Id, finding.SourceEntityId);
        Assert.Contains(serviceA.Id, finding.RelatedEntityIds);
        Assert.Contains(serviceB.Id, finding.RelatedEntityIds);
        Assert.Contains(task.Id, finding.RelatedEntityIds);
    }

    [Fact]
    public void ExactlyTwoWorkloadsShareOneExecutable_IsMergedIntoOneBoundary_NeverProducesFinding()
    {
        // Phase 5B merges exactly-two-anchor sharing into a single boundary (it's identity
        // evidence, not "shared infrastructure") — so it's never recorded as a SharedBinary
        // diagnostic and this rule must never flag it.
        var service = EntityFactory.Service("ERPWorker", @"D:\ERP\Worker\ERPWorker.exe");
        var workerExe = EntityFactory.Dll(@"D:\ERP\Worker\ERPWorker.exe");
        var task = EntityFactory.ScheduledTask(@"\ERP\Nightly", @"D:\ERP\Worker\ERPWorker.exe");

        var entities = new List<DiscoveryEntity> { service, workerExe, task };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void NoSharedExecutable_NeverProducesFinding()
    {
        var service = EntityFactory.Service("SoloSvc", @"C:\Solo\solo.exe");
        var soloExe = EntityFactory.Dll(@"C:\Solo\solo.exe");
        var entities = new List<DiscoveryEntity> { service, soloExe };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }
}
