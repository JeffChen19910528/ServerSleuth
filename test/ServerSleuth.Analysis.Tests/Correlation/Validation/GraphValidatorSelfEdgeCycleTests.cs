using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;

namespace ServerSleuth.Analysis.Tests.Correlation.Validation;

public class GraphValidatorSelfEdgeCycleTests
{
    private static DependencyExpansionResult Wrap(DependencyGraph graph) => new()
    {
        ExternalDependencies = [],
        ExpandedGraph = graph,
        DerivedWorkloadDependencies = [],
        Diagnostics = new ExpansionDiagnostics()
    };

    [Fact]
    public void Validate_SelfEdgeOfNonDependsOnType_IsClassifiedInvalid()
    {
        var app = EntityFactory.Application("A", "/", @"D:\A");
        var graph = new DependencyGraph();
        graph.AddNode(app);
        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = app.Id,
            TargetEntityId = app.Id,
            Type = DependencyEdgeType.Contains,
            Confidence = Confidence.Medium(),
            Evidence = [new EvidenceRecord { Type = EvidenceType.FileSystem, Location = "x" }]
        });

        var result = new GraphValidator().Validate([app], Wrap(graph), []);

        Assert.Contains(result.Findings, f => f.Code == "InvalidSelfEdge");
    }

    [Fact]
    public void Validate_SelfEdgeOfDependsOnType_IsClassifiedAsPotentialLegitimate()
    {
        var app = EntityFactory.Application("A", "/", @"D:\A");
        var graph = new DependencyGraph();
        graph.AddNode(app);
        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = app.Id,
            TargetEntityId = app.Id,
            Type = DependencyEdgeType.DependsOn,
            Confidence = Confidence.Medium(),
            Evidence = [new EvidenceRecord { Type = EvidenceType.FileSystem, Location = "x" }]
        });

        var result = new GraphValidator().Validate([app], Wrap(graph), []);

        Assert.Contains(result.Findings, f => f.Code == "PotentialLegitimateSelfReference");
        Assert.DoesNotContain(result.Findings, f => f.Code == "InvalidSelfEdge");
    }

    [Fact]
    public void Validate_ThreeNodeCycle_IsDetectedAndClassifiedStrong()
    {
        var a = EntityFactory.Dll(@"C:\A.dll");
        var b = EntityFactory.Dll(@"C:\B.dll");
        var c = EntityFactory.Dll(@"C:\C.dll");
        var graph = new DependencyGraph();
        graph.AddNode(a);
        graph.AddNode(b);
        graph.AddNode(c);

        DependencyEdge ImportEdge(string source, string target) => new()
        {
            SourceEntityId = source, TargetEntityId = target, Type = DependencyEdgeType.Imports,
            Confidence = Confidence.High(), Evidence = [new EvidenceRecord { Type = EvidenceType.PeMetadata, Location = source }]
        };

        graph.AddEdge(ImportEdge(a.Id, b.Id));
        graph.AddEdge(ImportEdge(b.Id, c.Id));
        graph.AddEdge(ImportEdge(c.Id, a.Id));

        var result = new GraphValidator().Validate([a, b, c], Wrap(graph), []);

        var cycle = Assert.Single(result.Cycles);
        Assert.Equal(3, cycle.NodeIds.Count);
        Assert.Equal(CycleClassification.Strong, cycle.Classification);
        Assert.Equal(1, result.Summary.Cycles);
    }

    [Fact]
    public void Validate_AcyclicGraph_ReportsNoCycles()
    {
        var a = EntityFactory.Dll(@"C:\A.dll");
        var b = EntityFactory.Dll(@"C:\B.dll");
        var graph = new DependencyGraph();
        graph.AddNode(a);
        graph.AddNode(b);
        graph.AddEdge(new DependencyEdge { SourceEntityId = a.Id, TargetEntityId = b.Id, Type = DependencyEdgeType.Imports, Confidence = Confidence.High(), Evidence = [new EvidenceRecord { Type = EvidenceType.PeMetadata, Location = a.Id }] });

        var result = new GraphValidator().Validate([a, b], Wrap(graph), []);

        Assert.Empty(result.Cycles);
    }

    [Fact]
    public void Validate_WeakOnlyCycle_IsClassifiedWeak()
    {
        var a = EntityFactory.Configuration(@"D:\A\a.config");
        var b = EntityFactory.Configuration(@"D:\B\b.config");
        var graph = new DependencyGraph();
        graph.AddNode(a);
        graph.AddNode(b);

        DependencyEdge RefEdge(string source, string target) => new()
        {
            SourceEntityId = source, TargetEntityId = target, Type = DependencyEdgeType.References,
            Confidence = Confidence.Low(), Evidence = [new EvidenceRecord { Type = EvidenceType.ConfigurationFile, Location = source }]
        };

        graph.AddEdge(RefEdge(a.Id, b.Id));
        graph.AddEdge(RefEdge(b.Id, a.Id));

        var result = new GraphValidator().Validate([a, b], Wrap(graph), []);

        var cycle = Assert.Single(result.Cycles);
        Assert.Equal(CycleClassification.Weak, cycle.Classification);
    }
}
