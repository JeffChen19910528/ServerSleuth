using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;

namespace ServerSleuth.Analysis.Tests.Correlation.Rules;

public class ApplicationConfiguresConfigurationRuleTests
{
    [Fact]
    public void Evaluate_ConfigurationOwnedByApplication_ProducesConfiguresCandidate()
    {
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP");
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", ownerEntityId: app.Id);
        var context = new CorrelationContext([app, config]);

        var candidates = new ApplicationConfiguresConfigurationRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Equal(app.Id, candidate.SourceEntityId);
        Assert.Equal(config.Id, candidate.TargetEntityId);
        Assert.Equal(DependencyEdgeType.Configures, candidate.Type);
    }

    [Fact]
    public void Evaluate_ConfigurationWithAmbiguousOwnership_ProducesNoCandidate()
    {
        // No OwnerEntityId set at all — ScanRootCollector could not unambiguously assign an owner.
        var config = EntityFactory.Configuration(@"D:\ERP\web.config");
        var context = new CorrelationContext([config]);

        var candidates = new ApplicationConfiguresConfigurationRule().Evaluate(context);

        Assert.Empty(candidates);
    }

    [Fact]
    public void Evaluate_ConfigurationOwnedBySiteNotApplication_ProducesNoCandidate()
    {
        var site = EntityFactory.Site("ERP");
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", ownerEntityId: site.Id);
        var context = new CorrelationContext([site, config]);

        var candidates = new ApplicationConfiguresConfigurationRule().Evaluate(context);

        Assert.Empty(candidates);
    }
}
