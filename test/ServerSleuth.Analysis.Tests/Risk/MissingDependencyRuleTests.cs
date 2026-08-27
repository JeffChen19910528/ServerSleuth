using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

public class MissingDependencyRuleTests
{
    private static readonly IReadOnlyList<Analysis.Risk.Rules.IRiskRule> OnlyThisRule = [new MissingDependencyRule()];

    [Fact]
    public void UnresolvedImport_ProducesHighFinding()
    {
        var importer = EntityFactory.Dll(@"D:\ERP\App.dll", importsCsv: "MissingVendor.dll");
        var entities = new List<DiscoveryEntity> { importer };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskCategory.MissingDependency, finding.Category);
        Assert.Equal(RiskSeverity.High, finding.Severity);
        Assert.Equal(importer.Id, finding.SourceEntityId);
    }

    [Fact]
    public void ResolvedImport_NeverProducesFinding()
    {
        var importer = EntityFactory.Dll(@"D:\ERP\App.dll", importsCsv: "Vendor.dll");
        var vendor = EntityFactory.Dll(@"D:\ERP\Vendor.dll");
        var entities = new List<DiscoveryEntity> { importer, vendor };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }
}
