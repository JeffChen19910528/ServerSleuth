using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;

namespace ServerSleuth.Analysis.Tests.Correlation.Rules;

public class ComReferencesBinaryRuleTests
{
    [Fact]
    public void Evaluate_InprocServer32MatchesBinary_ProducesReferencesCandidate()
    {
        var dll = EntityFactory.Dll(@"D:\ERP\VendorNative.dll");
        var com = EntityFactory.Com("{TEST-GUID}", inprocServer32: @"D:\ERP\VendorNative.dll");
        var context = new CorrelationContext([dll, com]);

        var candidates = new ComReferencesBinaryRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Equal(com.Id, candidate.SourceEntityId);
        Assert.Equal(dll.Id, candidate.TargetEntityId);
        Assert.Equal(DependencyEdgeType.References, candidate.Type);
    }

    [Fact]
    public void Evaluate_ComRegisteredButFileNotDiscovered_ProducesUnresolvedCandidate()
    {
        var com = EntityFactory.Com("{TEST-GUID}", inprocServer32: @"D:\ERP\Missing.dll");
        var context = new CorrelationContext([com]);

        var candidates = new ComReferencesBinaryRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Null(candidate.TargetEntityId);
    }

    [Fact]
    public void Evaluate_ComWithNoServerReference_ProducesNoCandidate()
    {
        var com = EntityFactory.Com("{TEST-GUID}");
        var context = new CorrelationContext([com]);

        var candidates = new ComReferencesBinaryRule().Evaluate(context);

        Assert.Empty(candidates);
    }
}
