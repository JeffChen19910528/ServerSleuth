using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

public class AccessDeniedRuleTests
{
    private static readonly IReadOnlyList<Analysis.Risk.Rules.IRiskRule> OnlyThisRule = [new AccessDeniedRule()];

    [Fact]
    public void ConfigurationAccessDenied_ProducesFinding_NeverClaimsComponentIsBroken()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config");
        config.SetMetadata("ParseStatus", "AccessDenied");
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskCategory.AccessDenied, finding.Category);
        Assert.Equal(Confidence.VeryHigh(), finding.Confidence);
        Assert.Contains("does not mean", finding.Description, StringComparison.OrdinalIgnoreCase); // explicitly disclaims "broken", never asserts it
        Assert.Contains("could not be", finding.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AccessDeniedConfiguration_MemberOfApplicationBoundary_EscalatesToHigh()
    {
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP");
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", ownerEntityId: app.Id);
        config.SetMetadata("ParseStatus", "AccessDenied");
        var entities = new List<DiscoveryEntity> { app, config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.High, finding.Severity);
        Assert.NotNull(finding.ApplicationBoundaryId);
    }

    [Fact]
    public void NormallyParsedConfiguration_NeverProducesFinding()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config");
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }
}
