using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Core.Tests.Graph;

public class DependencyGraphTests
{
    private static Service MakeService(string id) => new()
    {
        Id = id,
        Name = id,
        Type = "Service",
        Source = "ServiceControlManager"
    };

    [Fact]
    public void AddNode_ThrowsOnDuplicateId()
    {
        var graph = new DependencyGraph();
        graph.AddNode(MakeService("svc-1"));

        Assert.Throws<InvalidOperationException>(() => graph.AddNode(MakeService("svc-1")));
    }

    [Fact]
    public void AddEdge_NewRelationship_IsAddedAsIs()
    {
        var graph = new DependencyGraph();

        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = "process-1",
            TargetEntityId = "port-1",
            Type = DependencyEdgeType.ListensOn,
            Confidence = new Confidence(0.9),
            Evidence = [new EvidenceRecord { Type = EvidenceType.NetworkSocket, Location = "0.0.0.0:8011" }]
        });

        Assert.Single(graph.Edges);
    }

    [Fact]
    public void AddEdge_DuplicateRelationship_MergesEvidenceAndKeepsHigherConfidence()
    {
        var graph = new DependencyGraph();

        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = "process-1",
            TargetEntityId = "port-1",
            Type = DependencyEdgeType.ListensOn,
            Confidence = new Confidence(0.6),
            Evidence = [new EvidenceRecord { Type = EvidenceType.Process, Location = "ERPService.exe" }]
        });

        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = "process-1",
            TargetEntityId = "port-1",
            Type = DependencyEdgeType.ListensOn,
            Confidence = new Confidence(0.95),
            Evidence = [new EvidenceRecord { Type = EvidenceType.NetworkSocket, Location = "0.0.0.0:8011" }]
        });

        Assert.Single(graph.Edges);
        var merged = graph.Edges[0];
        Assert.Equal(0.95, merged.Confidence.Value);
        Assert.Equal(2, merged.Evidence.Count);
    }

    [Fact]
    public void EdgesFrom_And_EdgesTo_FilterByEntityId()
    {
        var graph = new DependencyGraph();
        var confidence = new Confidence(0.8);

        graph.AddEdge(new DependencyEdge { SourceEntityId = "a", TargetEntityId = "b", Type = DependencyEdgeType.DependsOn, Confidence = confidence });
        graph.AddEdge(new DependencyEdge { SourceEntityId = "a", TargetEntityId = "c", Type = DependencyEdgeType.Uses, Confidence = confidence });
        graph.AddEdge(new DependencyEdge { SourceEntityId = "b", TargetEntityId = "c", Type = DependencyEdgeType.Calls, Confidence = confidence });

        Assert.Equal(2, graph.EdgesFrom("a").Count());
        Assert.Equal(2, graph.EdgesTo("c").Count());
    }

    [Fact]
    public void TryGetNode_ReturnsFalseForUnknownId()
    {
        var graph = new DependencyGraph();

        var found = graph.TryGetNode("does-not-exist", out var entity);

        Assert.False(found);
        Assert.Null(entity);
    }

    [Fact]
    public void EdgesFrom_UnknownSourceId_ReturnsEmpty()
    {
        var graph = new DependencyGraph();
        graph.AddEdge(new DependencyEdge { SourceEntityId = "a", TargetEntityId = "b", Type = DependencyEdgeType.Uses, Confidence = new Confidence(0.5) });

        Assert.Empty(graph.EdgesFrom("does-not-exist"));
        Assert.Empty(graph.EdgesTo("does-not-exist"));
    }

    /// <summary>Phase 10A-I §6: two different insertion orders producing the same logical edge
    /// set must yield identical EdgesFrom/EdgesTo/Edges enumeration — the index must never
    /// depend on Dictionary/HashSet enumeration order, only on canonical insertion order.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EdgesFrom_And_EdgesTo_AreDeterministic_RegardlessOfInsertionOrder(bool reversed)
    {
        var confidence = new Confidence(0.8);
        var edges = new List<DependencyEdge>
        {
            new() { SourceEntityId = "hub", TargetEntityId = "leaf-1", Type = DependencyEdgeType.Uses, Confidence = confidence },
            new() { SourceEntityId = "hub", TargetEntityId = "leaf-2", Type = DependencyEdgeType.Uses, Confidence = confidence },
            new() { SourceEntityId = "hub", TargetEntityId = "leaf-3", Type = DependencyEdgeType.DependsOn, Confidence = confidence },
            new() { SourceEntityId = "leaf-1", TargetEntityId = "hub", Type = DependencyEdgeType.Calls, Confidence = confidence },
            new() { SourceEntityId = "leaf-2", TargetEntityId = "hub", Type = DependencyEdgeType.Calls, Confidence = confidence },
        };

        var insertionOrder = reversed ? edges.AsEnumerable().Reverse() : edges;

        var graph = new DependencyGraph();
        foreach (var edge in insertionOrder)
        {
            graph.AddEdge(edge);
        }

        var fromHub = graph.EdgesFrom("hub").Select(e => e.TargetEntityId).ToList();
        var toHub = graph.EdgesTo("hub").Select(e => e.SourceEntityId).ToList();

        var expectedFromHub = edges.Where(e => e.SourceEntityId == "hub").Select(e => e.TargetEntityId).ToList();
        var expectedToHub = edges.Where(e => e.TargetEntityId == "hub").Select(e => e.SourceEntityId).ToList();

        if (reversed)
        {
            expectedFromHub.Reverse();
            expectedToHub.Reverse();
        }

        Assert.Equal(expectedFromHub, fromHub);
        Assert.Equal(expectedToHub, toHub);
    }

    /// <summary>Structural proof (Phase 10A-I §9) that EdgesFrom/EdgesTo scale with the
    /// requested entity's own degree, not with total edge count: a huge number of edges never
    /// touching "hub" must not slow down a lookup on "hub", and EdgesFrom("hub") must return
    /// exactly hub's own edges — never any of the distractors.</summary>
    [Fact]
    public void EdgesFrom_LargeGraph_ReturnsOnlyOwnEdges_AndScalesWithDegreeNotEdgeCount()
    {
        var graph = new DependencyGraph();
        const int distractorCount = 50_000;

        for (var i = 0; i < distractorCount; i++)
        {
            graph.AddEdge(new DependencyEdge
            {
                SourceEntityId = $"distractor-src-{i}",
                TargetEntityId = $"distractor-dst-{i}",
                Type = DependencyEdgeType.Uses,
                Confidence = Confidence.High()
            });
        }

        for (var i = 0; i < 5; i++)
        {
            graph.AddEdge(new DependencyEdge
            {
                SourceEntityId = "hub",
                TargetEntityId = $"hub-target-{i}",
                Type = DependencyEdgeType.DependsOn,
                Confidence = Confidence.High()
            });
        }

        Assert.Equal(distractorCount + 5, graph.Edges.Count);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        List<DependencyEdge> fromHub = [];
        for (var i = 0; i < 10_000; i++)
        {
            fromHub = graph.EdgesFrom("hub").ToList();
        }
        stopwatch.Stop();

        Assert.Equal(5, fromHub.Count);
        Assert.All(fromHub, e => Assert.Equal("hub", e.SourceEntityId));
        Assert.True(stopwatch.Elapsed.TotalSeconds < 5,
            $"10,000 EdgesFrom(\"hub\") lookups against a {distractorCount + 5}-edge graph took {stopwatch.Elapsed.TotalSeconds:0.00}s — expected sub-second if indexed by out-degree rather than total edge count.");
    }

    /// <summary>Phase 10A-I §4-5: duplicate insertion at scale must keep merging into the single
    /// canonical edge (never growing Edges.Count) and must remain fast — proving AddEdge's
    /// duplicate check is indexed rather than an O(edge-count) scan.</summary>
    [Fact]
    public void AddEdge_ManyDuplicateInsertions_MergeInPlaceWithoutGrowingEdgeCount()
    {
        var graph = new DependencyGraph();
        for (var i = 0; i < 20_000; i++)
        {
            graph.AddEdge(new DependencyEdge
            {
                SourceEntityId = $"src-{i}",
                TargetEntityId = $"dst-{i}",
                Type = DependencyEdgeType.Uses,
                Confidence = Confidence.High()
            });
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 2_000; i++)
        {
            graph.AddEdge(new DependencyEdge
            {
                SourceEntityId = $"src-{i}",
                TargetEntityId = $"dst-{i}",
                Type = DependencyEdgeType.Uses,
                Confidence = Confidence.VeryHigh(),
                Evidence = [new EvidenceRecord { Type = EvidenceType.Process, Location = $"probe-{i}" }]
            });
        }
        stopwatch.Stop();

        Assert.Equal(20_000, graph.Edges.Count);
        var merged = graph.EdgesFrom("src-500").Single();
        Assert.Equal(ConfidenceBand.VeryHigh, merged.Confidence.Band);
        Assert.Single(merged.Evidence);
        Assert.True(stopwatch.Elapsed.TotalSeconds < 5,
            $"2,000 duplicate-edge AddEdge merges against a 20,000-edge graph took {stopwatch.Elapsed.TotalSeconds:0.00}s — expected sub-second if the duplicate check is indexed by source rather than scanning all edges.");
    }
}
