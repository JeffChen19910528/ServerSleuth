using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

public class MissingRuntimeRuleTests
{
    private static readonly IReadOnlyList<Analysis.Risk.Rules.IRiskRule> OnlyThisRule = [new MissingRuntimeRule()];

    [Fact]
    public void ExplicitNet8Requirement_NoMatchingRuntimeInstalled_ProducesFinding()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", dependencyReferences: ["RuntimeVersion: net8.0"]);
        var runtime6 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "6.0.25");
        var entities = new List<DiscoveryEntity> { config, runtime6 };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskCategory.MissingRuntime, finding.Category);
        Assert.Contains("net8.0", finding.Title);
    }

    [Fact]
    public void ExplicitNet8Requirement_MatchingRuntimeInstalled_NeverProducesFinding()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", dependencyReferences: ["RuntimeVersion: net8.0"]);
        var runtime8 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "8.0.10");
        var entities = new List<DiscoveryEntity> { config, runtime8 };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void ExplicitNet8Requirement_MatchingSdkInstalled_NeverProducesFinding()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", dependencyReferences: ["RuntimeVersion: net8.0"]);
        var sdk8 = EntityFactory.Sdk("DotNetSdk", ".NET SDK", "8.0.100");
        var entities = new List<DiscoveryEntity> { config, sdk8 };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void FamilyOnlyMarker_WithoutExplicitVersion_NeverProducesFinding()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", dependencyReferences: ["Runtime: DotNet"]);
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void UnusedInstalledRuntime_IsNeverFlagged()
    {
        var runtime10 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var entities = new List<DiscoveryEntity> { runtime10 };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }
}
