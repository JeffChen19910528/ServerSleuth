using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;

namespace ServerSleuth.Analysis.Tests.Correlation.Rules;

public class ServiceRunsBinaryRuleTests
{
    [Fact]
    public void Evaluate_QuotedImagePathWithArguments_ResolvesToBinary()
    {
        var dll = EntityFactory.Dll(@"D:\ERP\ERPWorker.exe");
        var service = EntityFactory.Service("ERPWorker", "\"D:\\ERP\\ERPWorker.exe\" --run");
        var context = new CorrelationContext([dll, service]);

        var candidates = new ServiceRunsBinaryRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Equal(service.Id, candidate.SourceEntityId);
        Assert.Equal(dll.Id, candidate.TargetEntityId);
        Assert.Equal(DependencyEdgeType.Runs, candidate.Type);
    }

    [Fact]
    public void Evaluate_MissingBinary_ProducesUnresolvedCandidateNotFound()
    {
        var service = EntityFactory.Service("Ghost", @"D:\ERP\DoesNotExist.exe");
        var context = new CorrelationContext([service]);

        var candidates = new ServiceRunsBinaryRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Null(candidate.TargetEntityId);
        Assert.NotNull(candidate.UnresolvedReason);
    }

    [Fact]
    public void Evaluate_AmbiguousUnquotedImagePath_ProducesUnresolvedCandidate()
    {
        var service = EntityFactory.Service("Ambiguous", @"D:\Program Files\Vendor\App.exe -k netsvcs");
        var context = new CorrelationContext([service]);

        var candidates = new ServiceRunsBinaryRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Null(candidate.TargetEntityId);
    }

    [Fact]
    public void Evaluate_NoExecutablePath_ProducesNoCandidate()
    {
        var service = EntityFactory.Service("NoPath", null);
        var context = new CorrelationContext([service]);

        var candidates = new ServiceRunsBinaryRule().Evaluate(context);

        Assert.Empty(candidates);
    }
}
