using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

public class ComDependencyRuleTests
{
    private static readonly IReadOnlyList<Analysis.Risk.Rules.IRiskRule> OnlyThisRule = [new ComDependencyRule()];

    [Fact]
    public void ComServerNeverResolved_ProducesHighFinding()
    {
        var com = EntityFactory.Com("{ABC}", inprocServer32: @"D:\ERP\Vendor.dll");
        var entities = new List<DiscoveryEntity> { com };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskCategory.Com, finding.Category);
        Assert.Equal(RiskSeverity.High, finding.Severity);
    }

    [Fact]
    public void ComServerMissingOnDisk_ProducesHighFinding_WithMergeAnchor()
    {
        var com = EntityFactory.Com("{ABC}", inprocServer32: @"D:\ERP\Vendor.dll");
        var dll = EntityFactory.Dll(@"D:\ERP\Vendor.dll", notFound: true);
        var entities = new List<DiscoveryEntity> { com, dll };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.High, finding.Severity);
        Assert.Equal(dll.Id, finding.Metadata["MissingBinaryEntityId"]);
    }

    [Fact]
    public void ComComponentWithNoServerReference_NeverProducesFinding()
    {
        var com = EntityFactory.Com("{ABC}");
        var entities = new List<DiscoveryEntity> { com };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void ComServerFoundOnDisk_NeverProducesFinding()
    {
        var com = EntityFactory.Com("{ABC}", inprocServer32: @"D:\ERP\Vendor.dll");
        var dll = EntityFactory.Dll(@"D:\ERP\Vendor.dll", notFound: false);
        var entities = new List<DiscoveryEntity> { com, dll };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }
}
