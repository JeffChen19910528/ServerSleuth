using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

public class ServiceDependencyRuleTests
{
    private static readonly IReadOnlyList<Analysis.Risk.Rules.IRiskRule> OnlyThisRule = [new ServiceDependencyRule()];

    [Fact]
    public void ServiceExecutableNeverResolved_ProducesCriticalFinding()
    {
        var service = EntityFactory.Service("ErpWorker", @"D:\ERP\Worker\ErpWorker.exe");
        var entities = new List<DiscoveryEntity> { service };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskCategory.Service, finding.Category);
        Assert.Equal(RiskSeverity.Critical, finding.Severity);
        Assert.Equal(service.Id, finding.SourceEntityId);
    }

    [Fact]
    public void ServiceExecutableMissingOnDisk_ProducesCriticalFinding_WithMergeAnchor()
    {
        var service = EntityFactory.Service("ErpWorker", @"D:\ERP\Worker\ErpWorker.exe");
        var dll = EntityFactory.Dll(@"D:\ERP\Worker\ErpWorker.exe", notFound: true);
        var entities = new List<DiscoveryEntity> { service, dll };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.Critical, finding.Severity);
        Assert.Contains(dll.Id, finding.RelatedEntityIds);
        Assert.Equal(dll.Id, finding.Metadata["MissingBinaryEntityId"]);
    }

    [Fact]
    public void ServiceExecutableFoundOnDisk_NeverProducesFinding()
    {
        var service = EntityFactory.Service("ErpWorker", @"D:\ERP\Worker\ErpWorker.exe");
        var dll = EntityFactory.Dll(@"D:\ERP\Worker\ErpWorker.exe", notFound: false);
        var entities = new List<DiscoveryEntity> { service, dll };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void ServiceWithNoExecutablePath_NeverProducesFinding()
    {
        var service = EntityFactory.Service("SomeService", null);
        var entities = new List<DiscoveryEntity> { service };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }
}
