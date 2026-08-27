using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Integration.Tests;

/// <summary>
/// Feeds a synthetic fixture containing BOTH Windows-shaped and Linux-shaped entities through
/// the exact same Analysis pipeline (CorrelationEngine → ApplicationBoundaryEngine →
/// DependencyExpansionEngine → GraphValidator) in one run — see skill.md (Phase 6G) §17. Proves
/// the Core entity vocabulary (Service/Configuration/Runtime/Dll/Certificate/ExternalDependency)
/// is genuinely shared, not accidentally Windows-shaped despite the type names. Entities are
/// constructed directly (not via real scanners) since the point here is the shared vocabulary
/// and the Analysis engines' behavior, not re-testing scanner discovery logic.
/// </summary>
public class CrossPlatformAnalysisFixtureTests
{
    private static (DependencyGraph Graph, List<ApplicationBoundary> Boundaries, GraphValidationResult Validation) RunPipeline(List<DiscoveryEntity> entities)
    {
        var graph = new CorrelationEngine().Correlate(entities).Graph;
        var boundaries = new ApplicationBoundaryEngine().Analyze(entities, graph).Boundaries.ToList();
        var expansion = new DependencyExpansionEngine().Expand(entities, graph, boundaries);
        var validation = new GraphValidator().Validate(entities, expansion, boundaries);
        return (graph, boundaries, validation);
    }

    private static List<DiscoveryEntity> BuildMixedPlatformFixture()
    {
        // --- Windows side ---
        var iisSite = new WebSite { Id = "iis-site:ERP", Name = "ERP", Type = "WebSite", Source = "IIS", PhysicalPath = @"D:\ERP\WebRoot", Status = EntityStatus.Running };
        var iisApp = new Application { Id = "iis-app:ERP/api", Name = "ERP/api", Type = "Application", Source = "IIS", Path = @"D:\ERP\WebRoot\api" };
        var windowsService = new Service
        {
            Id = "service:ErpWorker", Name = "ErpWorker", Type = "Service", Source = "ServiceControlManager",
            ExecutablePath = @"D:\ERP\Worker\ErpWorker.exe", Status = EntityStatus.Running
        };
        var windowsConfig = new Configuration
        {
            Id = @"configuration:D:\ERP\WebRoot\web.config", Name = "web.config", Type = "Configuration", Source = "FileSystem",
            Path = @"D:\ERP\WebRoot\web.config", Format = "Xml"
        };
        var windowsRuntime = new Runtime { Id = "runtime:dotnet:8.0.10", Name = ".NET Runtime 8.0.10", Type = "Runtime", Source = "Registry", Version = "8.0.10" };
        var windowsBinary = new Dll
        {
            Id = @"dll:D:\ERP\WebRoot\bin\Erp.Data.dll", Name = "Erp.Data.dll", Type = "ManagedDll", Source = "FileSystem",
            Path = @"D:\ERP\WebRoot\bin\Erp.Data.dll", Architecture = EntityArchitecture.X64
        };
        var windowsCertificate = new Certificate
        {
            Id = "certificate:ABC123THUMB", Name = "erp.example.com", Type = "Certificate", Source = "WindowsCertificateStore",
            Subject = "CN=erp.example.com", Thumbprint = "ABC123THUMB", ValidTo = DateTimeOffset.UtcNow.AddYears(1)
        };

        // --- Linux side ---
        var linuxService = new Service
        {
            Id = "service:erp-worker.service", Name = "erp-worker.service", Type = "Service", Source = "systemd",
            ExecutablePath = "/opt/erp/worker/erp-worker", Status = EntityStatus.Running
        };
        var linuxConfig = new Configuration
        {
            Id = "configuration:/opt/erp/worker/appsettings.json", Name = "appsettings.json", Type = "Configuration", Source = "FileSystem",
            Path = "/opt/erp/worker/appsettings.json", Format = "Json"
        };
        var linuxRuntime = new Runtime { Id = "runtime:dotnet:8.0.10-linux", Name = ".NET Runtime 8.0.10", Type = "Runtime", Source = "Command", Version = "8.0.10" };
        var linuxBinary = new Dll
        {
            Id = "dll:/opt/erp/worker/liberp.so", Name = "liberp.so", Type = "NativeBinary", Source = "FileSystem",
            Path = "/opt/erp/worker/liberp.so", Architecture = EntityArchitecture.X64
        };
        var linuxExternalDependency = new ExternalDependency
        {
            Id = "external:postgresql:db.internal:5432", Name = "PostgreSQL @ db.internal", Type = "ExternalDependency", Source = "ConfigurationFile",
            Kind = "Database", Endpoint = "db.internal:5432"
        };

        var entities = new List<DiscoveryEntity>
        {
            iisSite, iisApp, windowsService, windowsConfig, windowsRuntime, windowsBinary, windowsCertificate,
            linuxService, linuxConfig, linuxRuntime, linuxBinary, linuxExternalDependency
        };

        foreach (var entity in entities)
        {
            entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.FileSystem, Location = entity.Path ?? entity.Id });
            if (entity.Confidence == default)
            {
                entity.Confidence = Confidence.High();
            }
        }

        return entities;
    }

    [Fact]
    public void Pipeline_MixedWindowsAndLinuxEntities_NeverThrows_ProducesAGraph()
    {
        var entities = BuildMixedPlatformFixture();

        var (graph, boundaries, validation) = RunPipeline(entities);

        Assert.NotNull(graph);
        Assert.NotEmpty(boundaries);
        Assert.NotNull(validation);
    }

    [Fact]
    public void Pipeline_MixedWindowsAndLinuxEntities_EveryGraphNodeIsOneOfTheOriginalEntities_NeverFabricated()
    {
        var entities = BuildMixedPlatformFixture();
        var entityIds = entities.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

        var (graph, _, _) = RunPipeline(entities);

        Assert.All(graph.Nodes, node => Assert.Contains(node.Id, entityIds));
    }

    [Fact]
    public void Pipeline_MixedWindowsAndLinuxEntities_ProducesNoDanglingEdges()
    {
        var entities = BuildMixedPlatformFixture();
        var entityIds = entities.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

        var (graph, _, _) = RunPipeline(entities);

        Assert.All(graph.Edges, edge =>
        {
            Assert.Contains(edge.SourceEntityId, entityIds);
            Assert.Contains(edge.TargetEntityId, entityIds);
        });
    }

    [Fact]
    public void Pipeline_MixedWindowsAndLinuxEntities_NoConfidenceWithoutEvidence()
    {
        var entities = BuildMixedPlatformFixture();

        var (graph, _, _) = RunPipeline(entities);

        Assert.All(graph.Nodes, node =>
        {
            if (node.Confidence.Value > 0)
            {
                Assert.NotEmpty(node.Evidence);
            }
        });
    }

    [Fact]
    public void Pipeline_MixedWindowsAndLinuxEntities_ProducesNoErrorSeverityFindings()
    {
        var entities = BuildMixedPlatformFixture();

        var (_, _, validation) = RunPipeline(entities);

        Assert.DoesNotContain(validation.Findings, f => f.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Pipeline_RunTwiceOnSameMixedFixture_ProducesDeterministicResults()
    {
        var entitiesA = BuildMixedPlatformFixture();
        var entitiesB = BuildMixedPlatformFixture();

        var (graphA, boundariesA, _) = RunPipeline(entitiesA);
        var (graphB, boundariesB, _) = RunPipeline(entitiesB);

        Assert.Equal(graphA.Nodes.Select(n => n.Id).OrderBy(id => id, StringComparer.Ordinal),
                     graphB.Nodes.Select(n => n.Id).OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(boundariesA.Count, boundariesB.Count);
    }

    [Fact]
    public void Pipeline_WindowsAndLinuxServiceEntities_BothUseTheSameCoreServiceType_NeverPlatformSpecificSubtypes()
    {
        var entities = BuildMixedPlatformFixture();

        var services = entities.OfType<Service>().ToList();

        Assert.Equal(2, services.Count); // one Windows, one Linux — same Core.Models.Service type
        Assert.Contains(services, s => s.Source == "ServiceControlManager");
        Assert.Contains(services, s => s.Source == "systemd");
    }

    [Fact]
    public void Pipeline_WindowsAndLinuxBinaryEntities_BothUseTheSameCoreDllType_NeverPlatformSpecificSubtypes()
    {
        var entities = BuildMixedPlatformFixture();

        var binaries = entities.OfType<Dll>().ToList();

        Assert.Equal(2, binaries.Count); // ManagedDll (Windows) and NativeBinary (Linux) — same Core.Models.Dll type
        Assert.Contains(binaries, d => d.Type == "ManagedDll");
        Assert.Contains(binaries, d => d.Type == "NativeBinary");
    }
}
