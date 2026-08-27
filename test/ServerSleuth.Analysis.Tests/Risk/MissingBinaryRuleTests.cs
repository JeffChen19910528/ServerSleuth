using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

public class MissingBinaryRuleTests
{
    private static readonly IReadOnlyList<Analysis.Risk.Rules.IRiskRule> OnlyThisRule = [new MissingBinaryRule()];

    [Fact]
    public void ServiceRunsMissingBinary_ProducesCriticalFinding()
    {
        var service = EntityFactory.Service("ErpWorker", @"D:\ERP\Worker\ErpWorker.exe");
        var dll = EntityFactory.Dll(@"D:\ERP\Worker\ErpWorker.exe", notFound: true);
        var entities = new List<DiscoveryEntity> { service, dll };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskCategory.MissingBinary, finding.Category);
        Assert.Equal(RiskSeverity.Critical, finding.Severity);
        Assert.Equal(dll.Id, finding.SourceEntityId);
        Assert.Contains(service.Id, finding.RelatedEntityIds);
    }

    [Fact]
    public void ComReferencesMissingBinary_ProducesHighFinding()
    {
        var com = EntityFactory.Com("{ABC}", inprocServer32: @"D:\ERP\Vendor.dll");
        var dll = EntityFactory.Dll(@"D:\ERP\Vendor.dll", notFound: true);
        var entities = new List<DiscoveryEntity> { com, dll };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.High, finding.Severity);
    }

    [Fact]
    public void MissingBinaryWithNoDependents_ProducesNoFinding()
    {
        var dll = EntityFactory.Dll(@"D:\Orphan\NothingUsesThis.dll", notFound: true);
        var entities = new List<DiscoveryEntity> { dll };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void FoundBinary_NeverProducesFinding()
    {
        var service = EntityFactory.Service("ErpWorker", @"D:\ERP\Worker\ErpWorker.exe");
        var dll = EntityFactory.Dll(@"D:\ERP\Worker\ErpWorker.exe", notFound: false);
        var entities = new List<DiscoveryEntity> { service, dll };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }
}
