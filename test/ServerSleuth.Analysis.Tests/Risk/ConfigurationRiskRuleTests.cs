using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

public class ConfigurationRiskRuleTests
{
    private static readonly IReadOnlyList<Analysis.Risk.Rules.IRiskRule> OnlyThisRule = [new ConfigurationRiskRule()];

    [Fact]
    public void FileShareReference_ProducesHighFinding()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", dependencyReferences: [@"FileShare: \\fileserver01\ErpData"]);
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskCategory.Configuration, finding.Category);
        Assert.Equal(RiskSeverity.High, finding.Severity);
    }

    [Fact]
    public void NetworkStorageReference_ProducesHighFinding()
    {
        var config = EntityFactory.Configuration("/etc/erp/app.conf", dependencyReferences: ["NetworkStorage: nfs01:/exports/erp"]);
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.High, finding.Severity);
    }

    [Fact]
    public void UnixSocketReference_ProducesLowFinding()
    {
        var config = EntityFactory.Configuration("/etc/erp/app.conf", dependencyReferences: ["UnixSocket: /var/run/erp.sock"]);
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.Low, finding.Severity);
    }

    [Fact]
    public void EnvVarReference_ProducesInfoFinding()
    {
        var config = EntityFactory.Configuration("/etc/erp/app.conf", dependencyReferences: ["EnvVar: ERP_HOME"]);
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.Info, finding.Severity);
    }

    [Fact]
    public void EndpointAndDatabaseReferences_ProduceInfoFindings_DeferringToExternalDependencyRule()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", dependencyReferences:
        [
            "Endpoint: https://api.partner.example.com",
            "Database: sql01.internal/ErpDb"
        ]);
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Equal(2, result.Findings.Count);
        Assert.All(result.Findings, f => Assert.Equal(RiskSeverity.Info, f.Severity));
    }

    [Fact]
    public void UnrecognizedReferencePrefix_NeverProducesFinding()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", dependencyReferences: ["SomethingElse: whatever"]);
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities, OnlyThisRule);

        Assert.Empty(result.Findings);
    }
}
