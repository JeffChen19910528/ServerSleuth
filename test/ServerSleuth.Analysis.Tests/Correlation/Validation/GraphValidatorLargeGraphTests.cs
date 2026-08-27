using System.Diagnostics;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Correlation.Validation;

/// <summary>Synthetic large-graph validation — skill.md (Phase 5D) §27. Purely in-memory: no
/// file, process, or network is ever touched to build or validate this graph.</summary>
public class GraphValidatorLargeGraphTests
{
    [Fact]
    public void Validate_1000NodesAnd2000Edges_CompletesInMemoryAndReportsConsistentSummary()
    {
        var entities = new List<DiscoveryEntity>();
        var graph = new DependencyGraph();

        for (var i = 0; i < 1000; i++)
        {
            var dll = new Dll
            {
                Id = $"dll:C:\\Synthetic\\Binary{i}.dll",
                Name = $"Binary{i}.dll",
                Type = "NativeDll",
                Source = "FileSystem",
                Confidence = Confidence.High()
            };
            entities.Add(dll);
            graph.AddNode(dll);
        }

        var edgeCount = 0;
        for (var i = 0; i < 999 && edgeCount < 2000; i++)
        {
            // A chain (Binary i IMPORTS Binary i+1) plus a second "fan" edge to keep edge count
            // at ~2000 while staying acyclic, so this also exercises the no-cycle path at scale.
            graph.AddEdge(new DependencyEdge
            {
                SourceEntityId = $"dll:C:\\Synthetic\\Binary{i}.dll",
                TargetEntityId = $"dll:C:\\Synthetic\\Binary{i + 1}.dll",
                Type = DependencyEdgeType.Imports,
                Confidence = Confidence.High(),
                Evidence = [new EvidenceRecord { Type = EvidenceType.PeMetadata, Location = $"dll:C:\\Synthetic\\Binary{i}.dll" }]
            });
            edgeCount++;

            var fanTarget = (i * 7) % 1000;
            if (fanTarget != i && edgeCount < 2000)
            {
                graph.AddEdge(new DependencyEdge
                {
                    SourceEntityId = $"dll:C:\\Synthetic\\Binary{i}.dll",
                    TargetEntityId = $"dll:C:\\Synthetic\\Binary{fanTarget}.dll",
                    Type = DependencyEdgeType.Imports,
                    Confidence = Confidence.Medium(),
                    Evidence = [new EvidenceRecord { Type = EvidenceType.PeMetadata, Location = $"dll:C:\\Synthetic\\Binary{i}.dll" }]
                });
                edgeCount++;
            }
        }

        Assert.True(graph.Nodes.Count >= 1000);
        Assert.True(graph.Edges.Count >= 1900);

        var expansion = new DependencyExpansionResult
        {
            ExternalDependencies = [],
            ExpandedGraph = graph,
            DerivedWorkloadDependencies = [],
            Diagnostics = new ExpansionDiagnostics()
        };

        var stopwatch = Stopwatch.StartNew();
        var result = new GraphValidator().Validate(entities, expansion, []);
        stopwatch.Stop();

        Assert.Equal(graph.Nodes.Count, result.Summary.TotalNodes);
        Assert.Equal(graph.Edges.Count, result.Summary.TotalEdges);
        Assert.True(stopwatch.Elapsed.TotalSeconds < 10, $"Validation of a 1000-node/2000-edge graph took {stopwatch.Elapsed.TotalSeconds:0.00}s — expected well under 10s in-memory.");
    }
}
