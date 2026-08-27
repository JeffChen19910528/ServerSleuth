using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;

namespace ServerSleuth.Analysis.Tests.Correlation.Rules;

public class ApplicationUsesApplicationPoolRuleTests
{
    [Fact]
    public void Evaluate_ApplicationReferencesPool_ProducesUsesCandidate()
    {
        var pool = EntityFactory.ApplicationPool("ERPAppPool");
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP", poolId: pool.Id);
        var context = new CorrelationContext([pool, app]);

        var candidates = new ApplicationUsesApplicationPoolRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Equal(app.Id, candidate.SourceEntityId);
        Assert.Equal(pool.Id, candidate.TargetEntityId);
        Assert.Equal(DependencyEdgeType.Uses, candidate.Type);
    }

    [Fact]
    public void Evaluate_ApplicationWithoutPool_ProducesNoCandidate()
    {
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP");
        var context = new CorrelationContext([app]);

        var candidates = new ApplicationUsesApplicationPoolRule().Evaluate(context);

        Assert.Empty(candidates);
    }
}
