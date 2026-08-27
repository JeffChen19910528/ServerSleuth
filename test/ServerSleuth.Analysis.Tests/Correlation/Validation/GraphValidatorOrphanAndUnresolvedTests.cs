using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Correlation.Validation;

public class GraphValidatorOrphanAndUnresolvedTests
{
    private static DependencyExpansionResult Wrap(DependencyGraph graph) => new()
    {
        ExternalDependencies = [],
        ExpandedGraph = graph,
        DerivedWorkloadDependencies = [],
        Diagnostics = new ExpansionDiagnostics()
    };

    [Fact]
    public void Validate_IsolatedComRegistration_IsClassifiedExpectedOrphan()
    {
        var com = EntityFactory.Com("{GUID}");
        var graph = new DependencyGraph();
        graph.AddNode(com);

        var result = new GraphValidator().Validate([com], Wrap(graph), []);

        var orphan = Assert.Single(result.Orphans);
        Assert.Equal(OrphanClassification.Expected, orphan.Classification);
    }

    [Fact]
    public void Validate_IsolatedRuntime_IsClassifiedExpectedOrphan()
    {
        var runtime = EntityFactory.Runtime("Java", "OpenJDK", "17");
        var graph = new DependencyGraph();
        graph.AddNode(runtime);

        var result = new GraphValidator().Validate([runtime], Wrap(graph), []);

        Assert.Single(result.Orphans, o => o.Classification == OrphanClassification.Expected);
    }

    [Fact]
    public void Validate_IsolatedDll_IsClassifiedPotentialOrphan()
    {
        var dll = EntityFactory.Dll(@"D:\Somewhere\Loose.dll");
        var graph = new DependencyGraph();
        graph.AddNode(dll);

        var result = new GraphValidator().Validate([dll], Wrap(graph), []);

        var orphan = Assert.Single(result.Orphans);
        Assert.Equal(OrphanClassification.Potential, orphan.Classification);
    }

    [Fact]
    public void Validate_IsolatedService_IsClassifiedUnresolvedOrphan()
    {
        var service = EntityFactory.Service("LoneService", null);
        var graph = new DependencyGraph();
        graph.AddNode(service);

        var result = new GraphValidator().Validate([service], Wrap(graph), []);

        var orphan = Assert.Single(result.Orphans);
        Assert.Equal(OrphanClassification.Unresolved, orphan.Classification);
    }

    [Fact]
    public void Validate_ConnectedEntity_IsNeverReportedAsOrphan()
    {
        var app = EntityFactory.Application("A", "/", @"D:\A");
        var pool = EntityFactory.ApplicationPool("Pool");
        var graph = new DependencyGraph();
        graph.AddNode(app);
        graph.AddNode(pool);
        graph.AddEdge(new Core.Graph.DependencyEdge
        {
            SourceEntityId = app.Id, TargetEntityId = pool.Id, Type = Core.Enums.DependencyEdgeType.Uses,
            Confidence = Core.Evidence.Confidence.High(),
            Evidence = [new Core.Evidence.EvidenceRecord { Type = Core.Enums.EvidenceType.IisConfiguration, Location = "x" }]
        });

        var result = new GraphValidator().Validate([app, pool], Wrap(graph), []);

        Assert.Empty(result.Orphans);
    }

    [Fact]
    public void Validate_DllWithNotFoundFileStatus_ReportsMissingBinary()
    {
        var dll = EntityFactory.Dll(@"D:\Missing\Ghost.dll");
        dll.SetMetadata("FileStatus", "NotFound");
        var graph = new DependencyGraph();
        graph.AddNode(dll);

        var result = new GraphValidator().Validate([dll], Wrap(graph), []);

        Assert.Contains(result.Findings, f => f.Code == "MissingBinary" && f.EntityIds.Contains(dll.Id));
    }

    [Fact]
    public void Validate_UnresolvedPeImport_ReportsUnresolvedBinary()
    {
        var dll = EntityFactory.Dll(@"D:\App\App.dll", importsCsv: "SomeVendor.dll");
        var graph = new DependencyGraph();
        graph.AddNode(dll); // the imported "SomeVendor.dll" is never discovered as its own node

        var result = new GraphValidator().Validate([dll], Wrap(graph), []);

        Assert.Contains(result.Findings, f => f.Code == "UnresolvedBinary" && f.Message.Contains("SomeVendor.dll"));
    }
}
