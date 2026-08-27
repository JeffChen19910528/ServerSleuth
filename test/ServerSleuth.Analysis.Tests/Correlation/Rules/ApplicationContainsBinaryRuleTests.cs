using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;

namespace ServerSleuth.Analysis.Tests.Correlation.Rules;

public class ApplicationContainsBinaryRuleTests
{
    [Fact]
    public void Evaluate_BinaryReferencedByApplication_ProducesContainsCandidate_NeverDependsOn()
    {
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP");
        var dll = EntityFactory.Dll(@"D:\ERP\ERP.Web.dll", referencedBy: [app.Id]);
        var context = new CorrelationContext([app, dll]);

        var candidates = new ApplicationContainsBinaryRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Equal(app.Id, candidate.SourceEntityId);
        Assert.Equal(dll.Id, candidate.TargetEntityId);
        Assert.Equal(DependencyEdgeType.Contains, candidate.Type);
        Assert.NotEqual(ServerSleuth.Core.Evidence.ConfidenceBand.VeryHigh, candidate.Confidence.Band);
    }

    [Fact]
    public void Evaluate_BinaryMerelyLocatedInDirectory_WithNoOwnerReference_ProducesNoCandidate()
    {
        // A DLL discovered with no ReferencedByEntityIds at all (e.g. found only via a raw
        // directory listing, never actually tied to any application) must not be linked.
        var dll = EntityFactory.Dll(@"D:\ERP\Unrelated.dll");
        var context = new CorrelationContext([dll]);

        var candidates = new ApplicationContainsBinaryRule().Evaluate(context);

        Assert.Empty(candidates);
    }
}
