using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Correlation.Validation;

public class GraphValidatorNodeEdgeTests
{
    private static DependencyExpansionResult Wrap(DependencyGraph graph) => new()
    {
        ExternalDependencies = [],
        ExpandedGraph = graph,
        DerivedWorkloadDependencies = [],
        Diagnostics = new ExpansionDiagnostics()
    };

    [Fact]
    public void Validate_DuplicateIdInRawEntities_ReportsDuplicateNodeId()
    {
        var appA = EntityFactory.Application("ERP", "/", @"D:\ERP");
        var appB = EntityFactory.Application("ERP", "/", @"D:\ERP2"); // same Id, different content

        var graph = new DependencyGraph();
        graph.AddNode(appA); // only the first ever makes it into the graph

        var result = new GraphValidator().Validate([appA, appB], Wrap(graph), []);

        Assert.Contains(result.Findings, f => f.Code == "DuplicateNodeId" && f.EntityIds.Contains(appA.Id));
    }

    [Fact]
    public void Validate_EdgeWithDanglingSource_ReportsDanglingSource()
    {
        var target = EntityFactory.Application("ERP", "/", @"D:\ERP");
        var graph = new DependencyGraph();
        graph.AddNode(target);
        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = "does-not-exist",
            TargetEntityId = target.Id,
            Type = DependencyEdgeType.Hosts,
            Confidence = Confidence.VeryHigh(),
            Evidence = [new EvidenceRecord { Type = EvidenceType.IisConfiguration, Location = "x" }]
        });

        var result = new GraphValidator().Validate([target], Wrap(graph), []);

        Assert.Contains(result.Findings, f => f.Code == "DanglingSource");
        Assert.True(result.Summary.DanglingEdges >= 1);
    }

    [Fact]
    public void Validate_EdgeWithDanglingTarget_ReportsDanglingTarget()
    {
        var source = EntityFactory.Application("ERP", "/", @"D:\ERP");
        var graph = new DependencyGraph();
        graph.AddNode(source);
        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = source.Id,
            TargetEntityId = "does-not-exist",
            Type = DependencyEdgeType.Hosts,
            Confidence = Confidence.VeryHigh(),
            Evidence = [new EvidenceRecord { Type = EvidenceType.IisConfiguration, Location = "x" }]
        });

        var result = new GraphValidator().Validate([source], Wrap(graph), []);

        Assert.Contains(result.Findings, f => f.Code == "DanglingTarget");
    }

    [Fact]
    public void Validate_EdgeWithNoEvidence_ReportsMissingEvidence()
    {
        var a = EntityFactory.Application("A", "/", @"D:\A");
        var b = EntityFactory.ApplicationPool("Pool");
        var graph = new DependencyGraph();
        graph.AddNode(a);
        graph.AddNode(b);
        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = a.Id,
            TargetEntityId = b.Id,
            Type = DependencyEdgeType.Uses,
            Confidence = Confidence.High(),
            Evidence = []
        });

        var result = new GraphValidator().Validate([a, b], Wrap(graph), []);

        Assert.Contains(result.Findings, f => f.Code == "MissingEvidence");
        Assert.True(result.Summary.MissingEvidence >= 1);
    }

    [Fact]
    public void Validate_WellFormedGraph_NeverReportsDuplicateEdges()
    {
        // DependencyGraph.AddEdge always merges a matching Source/Target/Type triple — a
        // duplicate edge cannot be constructed through the public API. This test documents
        // that invariant rather than fabricating an unreachable state.
        var a = EntityFactory.Application("A", "/", @"D:\A");
        var b = EntityFactory.ApplicationPool("Pool");
        var graph = new DependencyGraph();
        graph.AddNode(a);
        graph.AddNode(b);
        graph.AddEdge(new DependencyEdge { SourceEntityId = a.Id, TargetEntityId = b.Id, Type = DependencyEdgeType.Uses, Confidence = Confidence.High(), Evidence = [new EvidenceRecord { Type = EvidenceType.IisConfiguration, Location = "x" }] });
        graph.AddEdge(new DependencyEdge { SourceEntityId = a.Id, TargetEntityId = b.Id, Type = DependencyEdgeType.Uses, Confidence = Confidence.VeryHigh(), Evidence = [new EvidenceRecord { Type = EvidenceType.IisConfiguration, Location = "y" }] });

        var result = new GraphValidator().Validate([a, b], Wrap(graph), []);

        Assert.Single(graph.Edges); // merged, not duplicated
        Assert.DoesNotContain(result.Findings, f => f.Code == "DuplicateEdge");
        Assert.Equal(0, result.Summary.DuplicateEdges);
    }

    [Fact]
    public void Validate_ComEdgeWithoutRegistryEvidence_ReportsInvalidEvidenceType()
    {
        var com = EntityFactory.Com("{GUID}", inprocServer32: @"D:\ERP\Vendor.dll");
        var dll = EntityFactory.Dll(@"D:\ERP\Vendor.dll");
        var graph = new DependencyGraph();
        graph.AddNode(com);
        graph.AddNode(dll);
        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = com.Id,
            TargetEntityId = dll.Id,
            Type = DependencyEdgeType.References,
            Confidence = Confidence.VeryHigh(),
            Evidence = [new EvidenceRecord { Type = EvidenceType.FileSystem, Location = dll.Id }] // wrong type for a COM edge
        });

        var result = new GraphValidator().Validate([com, dll], Wrap(graph), []);

        Assert.Contains(result.Findings, f => f.Code == "InvalidEvidenceType");
    }
}
