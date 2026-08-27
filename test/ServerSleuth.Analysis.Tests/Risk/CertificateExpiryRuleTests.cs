using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

public class CertificateExpiryRuleTests
{
    private static readonly IReadOnlyList<Analysis.Risk.Rules.IRiskRule> OnlyThisRule = [new CertificateExpiryRule()];

    [Fact]
    public void ExpiredCertificate_ProducesCriticalFinding()
    {
        var cert = EntityFactory.Certificate("erp.example.com", "AAA111", validTo: DateTimeOffset.UtcNow.AddDays(-5));
        var entities = new List<DiscoveryEntity> { cert };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.Critical, finding.Severity);
    }

    [Fact]
    public void ExpiringWithin30Days_ProducesHighFinding()
    {
        var cert = EntityFactory.Certificate("erp.example.com", "AAA111", validTo: DateTimeOffset.UtcNow.AddDays(10));
        var entities = new List<DiscoveryEntity> { cert };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.High, finding.Severity);
    }

    [Fact]
    public void ExpiringWithin90Days_ProducesMediumFinding()
    {
        var cert = EntityFactory.Certificate("erp.example.com", "AAA111", validTo: DateTimeOffset.UtcNow.AddDays(60));
        var entities = new List<DiscoveryEntity> { cert };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.Medium, finding.Severity);
    }

    [Fact]
    public void ValidForOverAYear_NeverProducesFinding()
    {
        var cert = EntityFactory.Certificate("erp.example.com", "AAA111", validTo: DateTimeOffset.UtcNow.AddYears(1));
        var entities = new List<DiscoveryEntity> { cert };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void UnknownExpiry_NeverGuessedAt_NeverProducesFinding()
    {
        var cert = EntityFactory.Certificate("erp.example.com", "AAA111");
        var entities = new List<DiscoveryEntity> { cert };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void NotYetValidCertificate_NeverFlaggedByThisRule()
    {
        var cert = EntityFactory.Certificate("erp.example.com", "AAA111", validTo: DateTimeOffset.UtcNow.AddYears(1), validFrom: DateTimeOffset.UtcNow.AddDays(10));
        var entities = new List<DiscoveryEntity> { cert };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }
}
