using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Tests.Correlation.Rules;

public class ConfigurationReferencesRuntimeRuleTests
{
    [Fact]
    public void Evaluate_RuntimeMarkerMatchesInstalledRuntime_ProducesLowConfidenceReferencesCandidate()
    {
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "8.0.0");
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", dependencyReferences: ["Runtime: DotNet"]);
        var context = new CorrelationContext([runtime, config]);

        var candidates = new ConfigurationReferencesRuntimeRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Equal(config.Id, candidate.SourceEntityId);
        Assert.Equal(runtime.Id, candidate.TargetEntityId);
        Assert.Equal(DependencyEdgeType.References, candidate.Type);
        Assert.Equal(ConfidenceBand.Low, candidate.Confidence.Band);
    }

    [Fact]
    public void Evaluate_RuntimeMarkerWithNoInstalledRuntime_ProducesUnresolvedCandidate()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", dependencyReferences: ["Runtime: Php"]);
        var context = new CorrelationContext([config]);

        var candidates = new ConfigurationReferencesRuntimeRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Null(candidate.TargetEntityId);
    }

    [Fact]
    public void Evaluate_InstalledRuntimeWithoutAnyApplicationReference_IsNotFalselyLinked()
    {
        // A runtime installed on the server with no configuration mentioning it must not
        // appear as a target of any candidate at all.
        var runtime = EntityFactory.Runtime("Java", "OpenJDK", "17");
        var config = EntityFactory.Configuration(@"D:\ERP\web.config");
        var context = new CorrelationContext([runtime, config]);

        var candidates = new ConfigurationReferencesRuntimeRule().Evaluate(context);

        Assert.Empty(candidates);
    }

    [Fact]
    public void Evaluate_DatabaseEndpointUncReferences_NeverProduceCandidates()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", dependencyReferences:
        [
            "Database: SqlServer@sqlserver.internal",
            "Endpoint: https://api.example.com",
            "FileShare: \\\\FILESERVER\\ERPData"
        ]);
        var context = new CorrelationContext([config]);

        var candidates = new ConfigurationReferencesRuntimeRule().Evaluate(context);

        Assert.Empty(candidates);
    }
}
