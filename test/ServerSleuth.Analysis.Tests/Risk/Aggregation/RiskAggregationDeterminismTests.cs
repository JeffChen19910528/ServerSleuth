using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk.Aggregation;

/// <summary>See skill.md (Phase 7B) §17, §21: determinism across repeated runs, and no
/// mutation of any input artifact.</summary>
public class RiskAggregationDeterminismTests
{
    private static (RiskAnalysisResult Result, RiskAnalysisContext Context) BuildPipeline()
    {
        var site = EntityFactory.Site("App1", @"D:\App1");
        var pool = EntityFactory.ApplicationPool("App1Pool");
        var app = EntityFactory.Application("App1", "/", @"D:\App1", poolId: pool.Id, siteId: site.Id);
        var config = EntityFactory.Configuration(@"D:\App1\web.config", ownerEntityId: app.Id);
        config.SetMetadata("ParseStatus", "AccessDenied");
        var missingDll = EntityFactory.Dll(@"D:\App1\vendor.dll", notFound: true);
        var cert = EntityFactory.Certificate("expiring.example.com", "DETCERT1", validTo: DateTimeOffset.UtcNow.AddDays(5));

        var entities = new List<DiscoveryEntity> { site, pool, app, config, missingDll, cert };
        return RiskPipeline.Run(entities);
    }

    [Fact]
    public void Aggregate_RepeatedRuns_ProduceIdenticalResults()
    {
        var (result, context) = BuildPipeline();
        var aggregator = new RiskAggregator();

        var runA = aggregator.Aggregate(context, result);
        var runB = aggregator.Aggregate(context, result);

        Assert.Equal(runA.Server.OverallSeverity, runB.Server.OverallSeverity);
        Assert.Equal(runA.Server.TotalFindingCount, runB.Server.TotalFindingCount);
        Assert.Equal(runA.Server.Findings.Select(f => f.Id), runB.Server.Findings.Select(f => f.Id));
        Assert.Equal(runA.Server.TopRisks.Select(f => f.Id), runB.Server.TopRisks.Select(f => f.Id));
        Assert.Equal(
            runA.Server.ApplicationSummaries.Select(s => (s.ApplicationBoundaryId, s.OverallSeverity, s.TotalFindingCount)),
            runB.Server.ApplicationSummaries.Select(s => (s.ApplicationBoundaryId, s.OverallSeverity, s.TotalFindingCount)));
        Assert.Equal(
            runA.Server.CategoryCounts.OrderBy(kv => kv.Key),
            runB.Server.CategoryCounts.OrderBy(kv => kv.Key));
        Assert.Equal(runA.Diagnostics.FindingsProcessed, runB.Diagnostics.FindingsProcessed);
        Assert.Equal(runA.Diagnostics.ApplicationSummariesCreated, runB.Diagnostics.ApplicationSummariesCreated);
        Assert.Equal(runA.Diagnostics.ServerLevelFindingCount, runB.Diagnostics.ServerLevelFindingCount);
    }

    [Fact]
    public void Aggregate_NeverMutatesInputs()
    {
        var (result, context) = BuildPipeline();

        var findingCountBefore = result.Findings.Count;
        var graphNodeCountBefore = context.Graph.Nodes.Count;
        var graphEdgeCountBefore = context.Graph.Edges.Count;
        var boundaryCountBefore = context.Boundaries.Count;
        var entityCountBefore = context.AllEntities.Count;

        new RiskAggregator().Aggregate(context, result);

        Assert.Equal(findingCountBefore, result.Findings.Count);
        Assert.Equal(graphNodeCountBefore, context.Graph.Nodes.Count);
        Assert.Equal(graphEdgeCountBefore, context.Graph.Edges.Count);
        Assert.Equal(boundaryCountBefore, context.Boundaries.Count);
        Assert.Equal(entityCountBefore, context.AllEntities.Count);
    }

    [Fact]
    public void Aggregate_ServerFindings_AreTheExactSameInstances_AsPhase7AOutput_NeverCopied()
    {
        var (result, context) = BuildPipeline();

        var aggregation = new RiskAggregator().Aggregate(context, result);

        // Reference equality, not just value equality — Aggregation must never re-copy or
        // re-identify RiskFinding instances (skill.md (Phase 7B) §1, §3).
        foreach (var finding in result.Findings)
        {
            Assert.Contains(aggregation.Server.Findings, f => ReferenceEquals(f, finding));
        }
    }
}
