using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Correlation.Validation;

public class GraphValidatorDomainSpecificTests
{
    private static DependencyExpansionResult Wrap(DependencyGraph graph, System.Collections.Generic.IReadOnlyList<ExternalDependency>? deps = null) => new()
    {
        ExternalDependencies = deps ?? [],
        ExpandedGraph = graph,
        DerivedWorkloadDependencies = [],
        Diagnostics = new ExpansionDiagnostics()
    };

    [Fact]
    public void Validate_TwoExternalDependenciesWithSameId_ReportsDuplicate()
    {
        var dep1 = new ExternalDependency { Id = "database:sqlserver:db01:1433:erp", Name = "DB01", Type = "ExternalDependency", Source = "Configuration", Confidence = Confidence.Medium(), Kind = "Database" };
        var dep2 = new ExternalDependency { Id = "database:sqlserver:db01:1433:erp", Name = "DB01 dup", Type = "ExternalDependency", Source = "Configuration", Confidence = Confidence.Medium(), Kind = "Database" };

        var result = new GraphValidator().Validate([], Wrap(new DependencyGraph(), [dep1, dep2]), []);

        Assert.Contains(result.Findings, f => f.Code == "DuplicateExternalDependencyId");
    }

    [Fact]
    public void Validate_TwoDependenciesSameHostPortDatabaseButDifferentIds_ReportsIdentityConflict()
    {
        var dep1 = new ExternalDependency { Id = "database:sqlserver:db01:1433:erp", Name = "a", Type = "ExternalDependency", Source = "Configuration", Confidence = Confidence.Medium(), Kind = "Database" };
        dep1.SetMetadata("Host", "DB01");
        dep1.SetMetadata("Port", "1433");
        dep1.SetMetadata("Database", "ERP");

        var dep2 = new ExternalDependency { Id = "database:sqlserver:db01different:1433:erp", Name = "b", Type = "ExternalDependency", Source = "Configuration", Confidence = Confidence.Medium(), Kind = "Database" };
        dep2.SetMetadata("Host", "DB01");
        dep2.SetMetadata("Port", "1433");
        dep2.SetMetadata("Database", "ERP");

        var result = new GraphValidator().Validate([], Wrap(new DependencyGraph(), [dep1, dep2]), []);

        Assert.Contains(result.Findings, f => f.Code == "ExternalDependencyIdentityConflict");
    }

    [Fact]
    public void Validate_IisBindingThumbprintWithNoMatchingCertificate_ReportsUnresolvedCertificate()
    {
        var site = EntityFactory.Site("ERP");
        EntityFactory.SetBinding(site, 0, "DEADBEEF");
        var graph = new DependencyGraph();
        graph.AddNode(site);

        var result = new GraphValidator().Validate([site], Wrap(graph), []);

        Assert.Contains(result.Findings, f => f.Code == "UnresolvedCertificate");
    }

    [Fact]
    public void Validate_IisBindingWithMatchingCertificate_ReportsNothing()
    {
        var site = EntityFactory.Site("ERP");
        EntityFactory.SetBinding(site, 0, "ABC123");
        var cert = EntityFactory.Certificate("LocalMachine\\My", "ABC123");
        var graph = new DependencyGraph();
        graph.AddNode(site);
        graph.AddNode(cert);
        graph.AddEdge(new DependencyEdge { SourceEntityId = site.Id, TargetEntityId = cert.Id, Type = DependencyEdgeType.Binds, Confidence = Confidence.VeryHigh(), Evidence = [new EvidenceRecord { Type = EvidenceType.IisConfiguration, Location = site.Id }] });

        var result = new GraphValidator().Validate([site, cert], Wrap(graph), []);

        Assert.DoesNotContain(result.Findings, f => f.Code == "UnresolvedCertificate");
    }

    [Fact]
    public void Validate_ComWithServerReferenceButNoReferencesEdge_ReportsUnresolvedComReference()
    {
        var com = EntityFactory.Com("{GUID}", inprocServer32: @"D:\Never\Discovered.dll");
        var graph = new DependencyGraph();
        graph.AddNode(com); // no REFERENCES edge was ever added for this com

        var result = new GraphValidator().Validate([com], Wrap(graph), []);

        Assert.Contains(result.Findings, f => f.Code == "UnresolvedComReference");
    }

    [Fact]
    public void Validate_ComReferencingMissingFile_ReportsComReferencesMissingFile()
    {
        var com = EntityFactory.Com("{GUID}", inprocServer32: @"D:\Ghost\Ghost.dll");
        var dll = EntityFactory.Dll(@"D:\Ghost\Ghost.dll");
        dll.SetMetadata("FileStatus", "NotFound");
        var graph = new DependencyGraph();
        graph.AddNode(com);
        graph.AddNode(dll);
        graph.AddEdge(new DependencyEdge { SourceEntityId = com.Id, TargetEntityId = dll.Id, Type = DependencyEdgeType.References, Confidence = Confidence.VeryHigh(), Evidence = [new EvidenceRecord { Type = EvidenceType.Registry, Location = com.Id }] });

        var result = new GraphValidator().Validate([com, dll], Wrap(graph), []);

        Assert.Contains(result.Findings, f => f.Code == "ComReferencesMissingFile");
    }

    [Fact]
    public void Validate_RuntimeEdgeWithInternallyInconsistentEvidence_ReportsRuntimeMismatch()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config");
        var runtime10 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var graph = new DependencyGraph();
        graph.AddNode(config);
        graph.AddNode(runtime10);

        // Deliberately doctored evidence: claims net8.0 matched, but names a 10.x runtime —
        // an internally inconsistent High-confidence runtime edge that should never occur from
        // the real DependencyExpansionEngine, but the validator must still catch it.
        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = config.Id,
            TargetEntityId = runtime10.Id,
            Type = DependencyEdgeType.References,
            Confidence = Confidence.High(),
            Evidence = [new EvidenceRecord { Type = EvidenceType.ConfigurationFile, Location = config.Id, Detail = "TargetFramework=net8.0 matched installed runtime version 10.0.0" }]
        });

        var result = new GraphValidator().Validate([config, runtime10], Wrap(graph), []);

        Assert.Contains(result.Findings, f => f.Code == "RuntimeMismatch");
    }

    [Fact]
    public void Validate_RuntimeEdgeWithConsistentEvidence_ReportsNoMismatch()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config");
        var runtime8 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "8.0.10");
        var graph = new DependencyGraph();
        graph.AddNode(config);
        graph.AddNode(runtime8);
        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = config.Id,
            TargetEntityId = runtime8.Id,
            Type = DependencyEdgeType.References,
            Confidence = Confidence.High(),
            Evidence = [new EvidenceRecord { Type = EvidenceType.ConfigurationFile, Location = config.Id, Detail = "TargetFramework=net8.0 matched installed runtime version 8.0.10" }]
        });

        var result = new GraphValidator().Validate([config, runtime8], Wrap(graph), []);

        Assert.DoesNotContain(result.Findings, f => f.Code == "RuntimeMismatch");
    }
}
