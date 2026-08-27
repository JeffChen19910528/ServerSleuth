using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;

namespace ServerSleuth.Analysis.Tests.Correlation.Validation;

public class GraphValidatorDeterminismAndMutationTests
{
    private static (DependencyGraph Graph, Core.Models.DiscoveryEntity[] Entities) BuildSampleGraph()
    {
        var app = EntityFactory.Application("A", "/", @"D:\A");
        var pool = EntityFactory.ApplicationPool("Pool");
        var orphanCom = EntityFactory.Com("{GUID}");
        var graph = new DependencyGraph();
        graph.AddNode(app);
        graph.AddNode(pool);
        graph.AddNode(orphanCom);
        graph.AddEdge(new DependencyEdge { SourceEntityId = app.Id, TargetEntityId = pool.Id, Type = DependencyEdgeType.Uses, Confidence = Confidence.High(), Evidence = [new EvidenceRecord { Type = EvidenceType.IisConfiguration, Location = "x" }] });

        return (graph, [app, pool, orphanCom]);
    }

    private static DependencyExpansionResult Wrap(DependencyGraph graph) => new()
    {
        ExternalDependencies = [],
        ExpandedGraph = graph,
        DerivedWorkloadDependencies = [],
        Diagnostics = new ExpansionDiagnostics()
    };

    [Fact]
    public void Validate_RunTwiceOnIdenticalInput_ProducesIdenticalFindingsOrphansAndCycles()
    {
        var (graph, entities) = BuildSampleGraph();
        var validator = new GraphValidator();

        var result1 = validator.Validate(entities, Wrap(graph), []);
        var result2 = validator.Validate(entities, Wrap(graph), []);

        Assert.Equal(
            result1.Findings.Select(f => (f.Category, f.Code, f.EntityIds.FirstOrDefault())),
            result2.Findings.Select(f => (f.Category, f.Code, f.EntityIds.FirstOrDefault())));

        Assert.Equal(result1.Orphans.Select(o => o.EntityId), result2.Orphans.Select(o => o.EntityId));
        Assert.Equal(result1.Cycles.Select(c => c.CycleId), result2.Cycles.Select(c => c.CycleId));
        Assert.Equal(result1.Summary, result2.Summary);
    }

    [Fact]
    public void Validate_NeverMutatesTheGraph()
    {
        var (graph, entities) = BuildSampleGraph();
        var nodeCountBefore = graph.Nodes.Count;
        var edgeCountBefore = graph.Edges.Count;

        _ = new GraphValidator().Validate(entities, Wrap(graph), []);

        Assert.Equal(nodeCountBefore, graph.Nodes.Count);
        Assert.Equal(edgeCountBefore, graph.Edges.Count);
    }
}
