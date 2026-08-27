using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;

namespace ServerSleuth.Analysis.Tests.Correlation.Rules;

public class IisBindingBindsToCertificateRuleTests
{
    [Fact]
    public void Evaluate_BindingThumbprintMatchesCertificate_ProducesBindsCandidate()
    {
        var site = EntityFactory.Site("ERP");
        EntityFactory.SetBinding(site, 0, "AABBCC");
        var certificate = EntityFactory.Certificate("LocalMachine\\My", "AABBCC");
        var context = new CorrelationContext([site, certificate]);

        var candidates = new IisBindingBindsToCertificateRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Equal(site.Id, candidate.SourceEntityId);
        Assert.Equal(certificate.Id, candidate.TargetEntityId);
        Assert.Equal(DependencyEdgeType.Binds, candidate.Type);
    }

    [Fact]
    public void Evaluate_ThumbprintCaseAndWhitespaceDifference_StillMatches()
    {
        var site = EntityFactory.Site("ERP");
        EntityFactory.SetBinding(site, 0, "aa bb cc");
        var certificate = EntityFactory.Certificate("LocalMachine\\My", "AABBCC");
        var context = new CorrelationContext([site, certificate]);

        var candidates = new IisBindingBindsToCertificateRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Equal(certificate.Id, candidate.TargetEntityId);
    }

    [Fact]
    public void Evaluate_NoMatchingCertificate_ProducesUnresolvedCandidate()
    {
        var site = EntityFactory.Site("ERP");
        EntityFactory.SetBinding(site, 0, "DEADBEEF");
        var context = new CorrelationContext([site]);

        var candidates = new IisBindingBindsToCertificateRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Null(candidate.TargetEntityId);
    }

    [Fact]
    public void Evaluate_NoBindings_ProducesNoCandidate()
    {
        var site = EntityFactory.Site("ERP");
        var context = new CorrelationContext([site]);

        var candidates = new IisBindingBindsToCertificateRule().Evaluate(context);

        Assert.Empty(candidates);
    }
}
