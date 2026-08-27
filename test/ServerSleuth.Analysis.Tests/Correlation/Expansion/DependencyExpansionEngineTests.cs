using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Correlation.Expansion;

public class DependencyExpansionEngineTests
{
    private static (List<DiscoveryEntity> Entities, Core.Graph.DependencyGraph Graph, List<Core.Boundaries.ApplicationBoundary> Boundaries) BuildErpFixture()
    {
        var site = EntityFactory.Site("ERP", @"D:\ERP\Web");
        var pool = EntityFactory.ApplicationPool("ERPAppPool");
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP\Web", poolId: pool.Id, siteId: site.Id);
        var webDll = EntityFactory.Dll(@"D:\ERP\Web\ERP.Web.dll", referencedBy: [app.Id]);
        var vendorNativeDll = EntityFactory.Dll(@"D:\ERP\Web\VendorNative.dll", referencedBy: [app.Id]);
        var com = EntityFactory.Com("{VENDOR-PDF-GUID}", inprocServer32: @"D:\ERP\Web\VendorNative.dll");

        EntityFactory.SetBinding(site, 0, "ABC123");
        var certificate = EntityFactory.Certificate("LocalMachine\\My", "ABC123");

        var runtime8 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "8.0.10");
        var runtime10 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");

        var config = EntityFactory.Configuration(@"D:\ERP\Web\web.config", ownerEntityId: app.Id,
            dependencyReferences: ["RuntimeVersion: net8.0"]);
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "ERP");
        config.SetMetadata("Database1.Type", "Redis");
        config.SetMetadata("Database1.Host", "CACHE01");
        config.SetMetadata("Database1.Port", "6379");
        config.SetMetadata("Endpoint0.Scheme", "https");
        config.SetMetadata("Endpoint0.Host", "api.example.com");
        config.SetMetadata("Endpoint0.Port", "443");
        config.SetMetadata("NetworkPath0.Server", "FILESERVER");
        config.SetMetadata("NetworkPath0.Share", "ERPData");

        var entities = new List<DiscoveryEntity>
        {
            site, pool, app, webDll, vendorNativeDll, com, certificate, runtime8, runtime10, config
        };

        var graph = new CorrelationEngine().Correlate(entities).Graph;
        var boundaries = new ApplicationBoundaryEngine().Analyze(entities, graph).Boundaries.ToList();

        return (entities, graph, boundaries);
    }

    [Fact]
    public void Expand_ConfigurationDatabaseReference_CreatesExternalDependencyAndBoundaryDependsOn()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var result = new DependencyExpansionEngine().Expand(entities, graph, boundaries);

        var db = Assert.Single(result.ExternalDependencies, d => d.Kind == ExternalDependencyKinds.Database);
        Assert.Equal("database:sqlserver:db01:1433:erp", db.Id);

        var boundary = boundaries.Single(b => b.MemberEntityIds.Contains("iis-application:ERP:/"));
        Assert.Contains(result.DerivedWorkloadDependencies, d => d.BoundaryId == boundary.Id && d.TargetEntityId == db.Id && d.Type == DependencyEdgeType.DependsOn);
    }

    [Fact]
    public void Expand_ConfigurationRedisReference_CreatesRedisExternalDependency()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var result = new DependencyExpansionEngine().Expand(entities, graph, boundaries);

        Assert.Contains(result.ExternalDependencies, d => d.Kind == ExternalDependencyKinds.Redis && d.Id == "redis:cache01:6379");
    }

    [Fact]
    public void Expand_ConfigurationApiReference_CreatesExternalApiDependency()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var result = new DependencyExpansionEngine().Expand(entities, graph, boundaries);

        Assert.Contains(result.ExternalDependencies, d => d.Kind == ExternalDependencyKinds.ExternalApi && d.Id == "api:https:api.example.com:443");
    }

    [Fact]
    public void Expand_ConfigurationUncReference_CreatesFileShareDependency()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var result = new DependencyExpansionEngine().Expand(entities, graph, boundaries);

        Assert.Contains(result.ExternalDependencies, d => d.Kind == ExternalDependencyKinds.FileShare && d.Id == @"fileshare:\\fileserver\erpdata");
    }

    [Fact]
    public void Expand_ExplicitTargetFrameworkNet8_LinksOnlyMatchingRuntimeVersion_NeverNet10()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var result = new DependencyExpansionEngine().Expand(entities, graph, boundaries);

        var config = entities.OfType<Configuration>().Single();
        var runtime8 = entities.OfType<Runtime>().Single(r => r.Version == "8.0.10");
        var runtime10 = entities.OfType<Runtime>().Single(r => r.Version == "10.0.0");

        Assert.Contains(result.ExpandedGraph.Edges, e =>
            e.SourceEntityId == config.Id && e.TargetEntityId == runtime8.Id && e.Type == DependencyEdgeType.References && e.Confidence.Band == ConfidenceBand.High);
        Assert.DoesNotContain(result.ExpandedGraph.Edges, e =>
            e.SourceEntityId == config.Id && e.TargetEntityId == runtime10.Id && e.Confidence.Band == ConfidenceBand.High);
    }

    [Fact]
    public void Expand_CertificateBoundToSite_ProducesBoundaryLevelDerivedBindsAssociation()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var result = new DependencyExpansionEngine().Expand(entities, graph, boundaries);

        var boundary = boundaries.Single(b => b.MemberEntityIds.Contains("iis-application:ERP:/"));
        var certificate = entities.OfType<Certificate>().Single();

        var derived = Assert.Single(result.DerivedWorkloadDependencies, d => d.Type == DependencyEdgeType.Binds);
        Assert.Equal(boundary.Id, derived.BoundaryId);
        Assert.Equal(certificate.Id, derived.TargetEntityId);
        Assert.Contains("BINDS_TO", derived.DerivedFrom, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Expand_ComReferencingBoundaryMemberBinary_ProducesDerivedComAssociation()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var result = new DependencyExpansionEngine().Expand(entities, graph, boundaries);

        var boundary = boundaries.Single(b => b.MemberEntityIds.Contains("iis-application:ERP:/"));
        var com = entities.OfType<ComComponent>().Single();

        Assert.Contains(result.DerivedWorkloadDependencies, d => d.BoundaryId == boundary.Id && d.TargetEntityId == com.Id && d.Type == DependencyEdgeType.References);
    }

    [Fact]
    public void Expand_LargeUnrelatedComPopulation_NeverAttachesToErpBoundary()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var mutableEntities = new List<DiscoveryEntity>(entities);

        for (var i = 0; i < 200; i++)
        {
            var unrelatedDll = EntityFactory.Dll($@"C:\Windows\System32\unrelated{i}.dll");
            var unrelatedCom = EntityFactory.Com($"{{UNRELATED-{i}}}", inprocServer32: $@"C:\Windows\System32\unrelated{i}.dll");
            mutableEntities.Add(unrelatedDll);
            mutableEntities.Add(unrelatedCom);
        }

        var fullGraph = new CorrelationEngine().Correlate(mutableEntities).Graph;
        var fullBoundaries = new ApplicationBoundaryEngine().Analyze(mutableEntities, fullGraph).Boundaries.ToList();
        var result = new DependencyExpansionEngine().Expand(mutableEntities, fullGraph, fullBoundaries);

        var erpBoundary = fullBoundaries.Single(b => b.MemberEntityIds.Contains("iis-application:ERP:/"));

        // ERP's boundary must only ever have the one legitimate COM association (Vendor.PDF) —
        // never any of the 200 unrelated registrations.
        var erpComTargets = result.DerivedWorkloadDependencies
            .Where(d => d.BoundaryId == erpBoundary.Id && d.Type == DependencyEdgeType.References && d.TargetEntityId.StartsWith("com:"))
            .Select(d => d.TargetEntityId)
            .ToList();

        Assert.Single(erpComTargets);
        Assert.Equal("com:LocalMachine:Registry64:{VENDOR-PDF-GUID}", erpComTargets[0]);
        Assert.True(result.Diagnostics.UnresolvedComRelationships.Count >= 200);
    }

    [Fact]
    public void Expand_DuplicateDatabaseAcrossTwoConfigFiles_MergesIntoOneEntityWithBothEvidence()
    {
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP\Web");
        var config1 = EntityFactory.Configuration(@"D:\ERP\Web\web.config", ownerEntityId: app.Id);
        config1.SetMetadata("Database0.Type", "SqlServer");
        config1.SetMetadata("Database0.Host", "DB01");
        config1.SetMetadata("Database0.Port", "1433");
        config1.SetMetadata("Database0.Name", "ERP");

        var config2 = EntityFactory.Configuration(@"D:\ERP\Worker\worker.config", ownerEntityId: app.Id);
        config2.SetMetadata("Database0.Type", "SqlServer");
        config2.SetMetadata("Database0.Host", "DB01");
        config2.SetMetadata("Database0.Port", "1433");
        config2.SetMetadata("Database0.Name", "ERP");

        var entities = new List<DiscoveryEntity> { app, config1, config2 };
        var graph = new CorrelationEngine().Correlate(entities).Graph;
        var boundaries = new ApplicationBoundaryEngine().Analyze(entities, graph).Boundaries.ToList();

        var result = new DependencyExpansionEngine().Expand(entities, graph, boundaries);

        var db = Assert.Single(result.ExternalDependencies);
        Assert.True(db.Evidence.Count >= 2);
        Assert.Equal(1, result.Diagnostics.ExternalDependenciesCreated);
        Assert.Equal(1, result.Diagnostics.ExternalDependenciesMerged);
    }

    [Fact]
    public void Expand_GivenIdenticalInputTwice_ProducesDeterministicExternalDependencyIds()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var engine = new DependencyExpansionEngine();

        var result1 = engine.Expand(entities, graph, boundaries);
        var result2 = engine.Expand(entities, graph, boundaries);

        Assert.Equal(
            result1.ExternalDependencies.Select(d => d.Id).OrderBy(x => x),
            result2.ExternalDependencies.Select(d => d.Id).OrderBy(x => x));
    }

    [Fact]
    public void Expand_DerivedDependency_PreservesFullProvenanceChain()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var result = new DependencyExpansionEngine().Expand(entities, graph, boundaries);

        var derived = result.DerivedWorkloadDependencies.First(d => d.Type == DependencyEdgeType.DependsOn);
        Assert.Contains("ApplicationBoundary", derived.DerivedFrom);
        Assert.Contains("Configuration", derived.DerivedFrom);
        Assert.Contains("REFERENCES", derived.DerivedFrom);
        Assert.NotEmpty(derived.Evidence);
    }

    [Fact]
    public void Expand_ExternalDependencyMetadata_NeverContainsSecretLookingValues()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var result = new DependencyExpansionEngine().Expand(entities, graph, boundaries);

        foreach (var dependency in result.ExternalDependencies)
        {
            foreach (var value in dependency.Metadata.Values)
            {
                Assert.DoesNotContain("Password", value, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Secret", value, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>Phase 10A-I §7: the expanded graph is built via <c>CloneGraph</c>, which must
    /// produce fully independent lookup indexes from the original graph's — mutating the
    /// original after expansion must never leak into the already-produced expanded graph.</summary>
    [Fact]
    public void Expand_MutatingOriginalGraphAfterExpansion_NeverAffectsExpandedGraph()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var originalEdgeCountBefore = graph.Edges.Count;

        var result = new DependencyExpansionEngine().Expand(entities, graph, boundaries);
        var expandedEdgeCountBefore = result.ExpandedGraph.Edges.Count;

        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = "probe-source",
            TargetEntityId = "probe-target",
            Type = DependencyEdgeType.DependsOn,
            Confidence = Confidence.High()
        });

        Assert.Equal(originalEdgeCountBefore + 1, graph.Edges.Count);
        Assert.Equal(expandedEdgeCountBefore, result.ExpandedGraph.Edges.Count);
        Assert.DoesNotContain(result.ExpandedGraph.Edges, e => e.SourceEntityId == "probe-source");
        Assert.Empty(result.ExpandedGraph.EdgesFrom("probe-source"));
    }

    /// <summary>Same independence guarantee in the other direction: mutating the clone
    /// (expanded graph) must never leak back into the original graph the caller still holds.</summary>
    [Fact]
    public void Expand_MutatingExpandedGraphAfterExpansion_NeverAffectsOriginalGraph()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var originalEdgeCountBefore = graph.Edges.Count;

        var result = new DependencyExpansionEngine().Expand(entities, graph, boundaries);

        result.ExpandedGraph.AddEdge(new DependencyEdge
        {
            SourceEntityId = "probe-source-2",
            TargetEntityId = "probe-target-2",
            Type = DependencyEdgeType.DependsOn,
            Confidence = Confidence.High()
        });

        Assert.Equal(originalEdgeCountBefore, graph.Edges.Count);
        Assert.DoesNotContain(graph.Edges, e => e.SourceEntityId == "probe-source-2");
        Assert.Empty(graph.EdgesFrom("probe-source-2"));
    }

    /// <summary>Phase 10A-I §10-11 performance regression: BuildComAssociations must scale with
    /// candidate COM relationships, not Boundaries * ComComponents. A few thousand unrelated COM
    /// registrations spread across many boundaries must resolve well under the old >60s bound.</summary>
    [Fact]
    public void Expand_ManyBoundariesAndManyUnrelatedComComponents_CompletesQuicklyAndOnlyAssociatesRealMatches()
    {
        var (entities, graph, boundaries) = BuildErpFixture();
        var mutableEntities = new List<DiscoveryEntity>(entities);

        for (var i = 0; i < 50; i++)
        {
            var site = EntityFactory.Site($"App{i}", $@"D:\App{i}\Web");
            var app = EntityFactory.Application($"App{i}", "/", $@"D:\App{i}\Web", siteId: site.Id);
            var dll = EntityFactory.Dll($@"D:\App{i}\Web\App{i}.dll", referencedBy: [app.Id]);
            mutableEntities.Add(site);
            mutableEntities.Add(app);
            mutableEntities.Add(dll);
        }

        for (var i = 0; i < 3_000; i++)
        {
            var unrelatedDll = EntityFactory.Dll($@"C:\Windows\System32\perf-unrelated{i}.dll");
            var unrelatedCom = EntityFactory.Com($"{{PERF-UNRELATED-{i}}}", inprocServer32: $@"C:\Windows\System32\perf-unrelated{i}.dll");
            mutableEntities.Add(unrelatedDll);
            mutableEntities.Add(unrelatedCom);
        }

        var fullGraph = new CorrelationEngine().Correlate(mutableEntities).Graph;
        var fullBoundaries = new ApplicationBoundaryEngine().Analyze(mutableEntities, fullGraph).Boundaries.ToList();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new DependencyExpansionEngine().Expand(mutableEntities, fullGraph, fullBoundaries);
        stopwatch.Stop();

        var erpBoundary = fullBoundaries.Single(b => b.MemberEntityIds.Contains("iis-application:ERP:/"));
        var erpComTargets = result.DerivedWorkloadDependencies
            .Where(d => d.BoundaryId == erpBoundary.Id && d.Type == DependencyEdgeType.References && d.TargetEntityId.StartsWith("com:"))
            .Select(d => d.TargetEntityId)
            .ToList();

        Assert.Single(erpComTargets);
        Assert.Equal("com:LocalMachine:Registry64:{VENDOR-PDF-GUID}", erpComTargets[0]);
        Assert.True(result.Diagnostics.UnresolvedComRelationships.Count >= 3_000);
        Assert.True(stopwatch.Elapsed.TotalSeconds < 10,
            $"Expansion across {fullBoundaries.Count} boundaries and {mutableEntities.OfType<ComComponent>().Count()} COM components took {stopwatch.Elapsed.TotalSeconds:0.00}s — expected well under the old >60s timeout.");
    }

    [Fact]
    public void Expand_FamilyOnlyRuntimeMarkerWithMultipleInstalledVersions_NeverAddsHighConfidenceSelection()
    {
        var runtime6 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "6.0.25");
        var runtime8 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "8.0.10");
        var runtime10 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP\Web");
        var config = EntityFactory.Configuration(@"D:\ERP\Web\web.config", ownerEntityId: app.Id,
            dependencyReferences: ["Runtime: DotNet"]); // family-only, no explicit version

        var entities = new List<DiscoveryEntity> { runtime6, runtime8, runtime10, app, config };
        var graph = new CorrelationEngine().Correlate(entities).Graph;
        var boundaries = new ApplicationBoundaryEngine().Analyze(entities, graph).Boundaries.ToList();

        var result = new DependencyExpansionEngine().Expand(entities, graph, boundaries);

        Assert.DoesNotContain(result.ExpandedGraph.Edges, e =>
            e.SourceEntityId == config.Id && e.Type == DependencyEdgeType.References && e.Confidence.Band == ConfidenceBand.High);
    }
}
