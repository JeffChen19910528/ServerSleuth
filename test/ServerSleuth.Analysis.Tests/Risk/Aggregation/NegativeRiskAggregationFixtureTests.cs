using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk.Aggregation;

/// <summary>
/// Negative fixtures at the Aggregation layer (skill.md Phase 7B §20) — reuses the exact
/// zero-finding scenarios from Phase 7A's <c>NegativeRiskFixtureTests</c> and confirms an empty
/// <c>RiskAnalysisResult</c> aggregates to <see cref="AggregateSeverity.None"/> with zero
/// summaries, never a fabricated risk. Also confirms shared-parent-directory and same-named-
/// unrelated-DLL scenarios (which never produce a SharedInfrastructure finding at the Phase 7A
/// layer) correctly never inflate <c>SharedDependencyCount</c> at the aggregate layer either.
/// </summary>
public class NegativeRiskAggregationFixtureTests
{
    private static RiskAggregationResult RunAndAggregate(List<DiscoveryEntity> entities)
    {
        var (result, context) = RiskPipeline.Run(entities);
        return new RiskAggregator().Aggregate(context, result);
    }

    [Fact]
    public void ExpectedOrphanRuntime_AggregatesToNone()
    {
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var aggregation = RunAndAggregate([runtime]);

        Assert.Equal(AggregateSeverity.None, aggregation.Server.OverallSeverity);
        Assert.Equal(0, aggregation.Server.TotalFindingCount);
        Assert.Empty(aggregation.Server.ApplicationSummaries);
    }

    [Fact]
    public void ExpectedOrphanCertificate_AggregatesToNone()
    {
        var certificate = EntityFactory.Certificate("unused.example.com", "ORPHAN001", validTo: DateTimeOffset.UtcNow.AddYears(2));
        var aggregation = RunAndAggregate([certificate]);

        Assert.Equal(AggregateSeverity.None, aggregation.Server.OverallSeverity);
        Assert.Equal(0, aggregation.Server.TotalFindingCount);
    }

    [Fact]
    public void SharedParentDirectoryWithoutSharedExecutable_NeverInflatesSharedDependencyCount()
    {
        var serviceA = EntityFactory.Service("SvcA", @"D:\Shared\AppA\a.exe");
        var exeA = EntityFactory.Dll(@"D:\Shared\AppA\a.exe");
        var serviceB = EntityFactory.Service("SvcB", @"D:\Shared\AppB\b.exe");
        var exeB = EntityFactory.Dll(@"D:\Shared\AppB\b.exe");

        var aggregation = RunAndAggregate([serviceA, exeA, serviceB, exeB]);

        Assert.Equal(0, aggregation.Server.SharedDependencyCount);
        Assert.Equal(AggregateSeverity.None, aggregation.Server.OverallSeverity);
    }

    [Fact]
    public void SameNamedDllsInUnrelatedApplicationRoots_NeverMergedIntoOneApplicationSummary()
    {
        var serviceA = EntityFactory.Service("AlphaWorker", @"D:\Alpha\worker.exe");
        var alphaExe = EntityFactory.Dll(@"D:\Alpha\worker.exe");
        var serviceB = EntityFactory.Service("BetaWorker", @"E:\Beta\worker.exe");
        var betaExe = EntityFactory.Dll(@"E:\Beta\worker.exe");

        var aggregation = RunAndAggregate([serviceA, alphaExe, serviceB, betaExe]);

        Assert.Empty(aggregation.Server.ApplicationSummaries);
        Assert.Equal(0, aggregation.Server.SharedDependencyCount);
    }

    [Fact]
    public void FamilyOnlyRuntimeMarkerWithoutExplicitVersion_AggregatesToNone()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", dependencyReferences: ["Runtime: DotNet"]);
        var aggregation = RunAndAggregate([config]);

        Assert.Equal(AggregateSeverity.None, aggregation.Server.OverallSeverity);
        Assert.Empty(aggregation.Server.CategoryCounts);
    }

    [Fact]
    public void EmptyEntitySet_AggregatesToNone_WithZeroConfidence()
    {
        var aggregation = RunAndAggregate([]);

        Assert.Equal(AggregateSeverity.None, aggregation.Server.OverallSeverity);
        Assert.Equal(0, aggregation.Server.TotalFindingCount);
        Assert.Equal(0.0, aggregation.Server.AggregateConfidence.Value);
        Assert.Equal(0, aggregation.Server.AffectedBoundaryCount);
        Assert.Equal(0, aggregation.Server.AffectedEntityCount);
    }
}
