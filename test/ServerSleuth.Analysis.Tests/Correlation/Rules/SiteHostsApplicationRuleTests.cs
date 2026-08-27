using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;

namespace ServerSleuth.Analysis.Tests.Correlation.Rules;

public class SiteHostsApplicationRuleTests
{
    [Fact]
    public void Evaluate_ApplicationReferencesSite_ProducesHostsCandidate()
    {
        var site = EntityFactory.Site("ERP");
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP");
        var context = new CorrelationContext([site, app]);

        var candidates = new SiteHostsApplicationRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Equal(site.Id, candidate.SourceEntityId);
        Assert.Equal(app.Id, candidate.TargetEntityId);
        Assert.Equal(DependencyEdgeType.Hosts, candidate.Type);
        Assert.NotEmpty(candidate.Evidence);
    }

    [Fact]
    public void Evaluate_ApplicationWithNoMatchingSite_ProducesNoCandidate()
    {
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP");
        var context = new CorrelationContext([app]);

        var candidates = new SiteHostsApplicationRule().Evaluate(context);

        Assert.Empty(candidates);
    }
}
