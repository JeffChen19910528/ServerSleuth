using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

public class ExternalDependencyRuleTests
{
    private static readonly IReadOnlyList<Analysis.Risk.Rules.IRiskRule> OnlyThisRule = [new ExternalDependencyRule()];

    [Fact]
    public void ExternalDatabase_ProducesMediumFinding()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config");
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "sql01.internal");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "ErpDb");
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskCategory.ExternalDependency, finding.Category);
        Assert.Equal(RiskSeverity.Medium, finding.Severity);
        Assert.DoesNotContain("Password", finding.Description);
    }

    [Fact]
    public void ExternalFileShare_ProducesHighFinding()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config");
        config.SetMetadata("NetworkPath0.Server", "fileserver01");
        config.SetMetadata("NetworkPath0.Share", "ErpData");
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.High, finding.Severity);
    }

    [Fact]
    public void ExternalLdapEndpoint_ProducesHighFinding()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config");
        config.SetMetadata("Endpoint0.Scheme", "ldap");
        config.SetMetadata("Endpoint0.Host", "dc01.internal");
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.High, finding.Severity);
    }

    [Fact]
    public void ExternalHttpsApiEndpoint_ProducesMediumFinding()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config");
        config.SetMetadata("Endpoint0.Scheme", "https");
        config.SetMetadata("Endpoint0.Host", "api.partner.example.com");
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.Medium, finding.Severity);
    }

    [Fact]
    public void NoExternalDependencies_NeverProducesFinding()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config");
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }
}
