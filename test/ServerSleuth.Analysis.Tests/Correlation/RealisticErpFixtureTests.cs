using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Correlation;

/// <summary>
/// End-to-end synthetic legacy-enterprise-server scenario (skill.md §22): IIS Site "ERP" with
/// one Application, an Application Pool, a web.config, a managed and a native DLL, a COM
/// registration pointing at the native DLL, a certificate bound to the site, a Windows Service
/// and a Scheduled Task both running the same worker executable, and configuration references
/// to SQL Server/an HTTPS endpoint/a UNC share/.NET. Only evidence-backed relationships should
/// appear in the resulting graph.
/// </summary>
public class RealisticErpFixtureTests
{
    private static (CorrelationResult Result, Dictionary<string, DiscoveryEntity> Entities) BuildAndCorrelate()
    {
        var site = EntityFactory.Site("ERP", @"D:\ERP");
        var pool = EntityFactory.ApplicationPool("ERPAppPool");
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP", poolId: pool.Id, siteId: site.Id);
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", ownerEntityId: app.Id,
            dependencyReferences:
            [
                "Database: SqlServer@sqlserver.internal",
                "Endpoint: https://api.example.com",
                "FileShare: \\\\FILESERVER\\ERPData",
                "Runtime: DotNet"
            ]);
        var managedDll = EntityFactory.Dll(@"D:\ERP\ERP.Web.dll", referencedBy: [app.Id]);
        var nativeDll = EntityFactory.Dll(@"D:\ERP\VendorNative.dll", referencedBy: [app.Id]);
        var com = EntityFactory.Com("{TEST-GUID}", inprocServer32: @"D:\ERP\VendorNative.dll");
        var certificate = EntityFactory.Certificate("LocalMachine\\My", "TEST");
        EntityFactory.SetBinding(site, 0, "TEST");
        var service = EntityFactory.Service("ERPWorker", @"D:\ERP\ERPWorker.exe");
        var workerExe = EntityFactory.Dll(@"D:\ERP\ERPWorker.exe", referencedBy: [app.Id]);
        var task = EntityFactory.ScheduledTask(@"\ERP\Nightly", @"D:\ERP\ERPWorker.exe");
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "8.0.0");

        var entities = new List<DiscoveryEntity>
        {
            site, pool, app, config, managedDll, nativeDll, com, certificate, service, workerExe, task, runtime
        };

        var result = new CorrelationEngine().Correlate(entities);
        return (result, entities.ToDictionary(e => e.Id));
    }

    [Fact]
    public void Correlate_ErpScenario_SiteHostsApplication()
    {
        var (result, entities) = BuildAndCorrelate();

        Assert.Contains(result.Graph.Edges, e =>
            e.SourceEntityId == "iis-site:ERP" && e.TargetEntityId == entities.Values.OfType<Application>().Single().Id &&
            e.Type == DependencyEdgeType.Hosts);
    }

    [Fact]
    public void Correlate_ErpScenario_ApplicationUsesPool()
    {
        var (result, _) = BuildAndCorrelate();

        Assert.Contains(result.Graph.Edges, e => e.Type == DependencyEdgeType.Uses && e.TargetEntityId == "iis-apppool:ERPAppPool");
    }

    [Fact]
    public void Correlate_ErpScenario_ApplicationConfiguresWebConfig()
    {
        var (result, _) = BuildAndCorrelate();

        Assert.Contains(result.Graph.Edges, e => e.Type == DependencyEdgeType.Configures && e.TargetEntityId.Contains("web.config"));
    }

    [Fact]
    public void Correlate_ErpScenario_ApplicationContainsAllThreeBinaries()
    {
        var (result, _) = BuildAndCorrelate();

        var containsEdges = result.Graph.Edges.Where(e => e.Type == DependencyEdgeType.Contains).ToList();
        Assert.Equal(3, containsEdges.Count);
    }

    [Fact]
    public void Correlate_ErpScenario_ServiceAndTaskBothRunWorkerExe()
    {
        var (result, _) = BuildAndCorrelate();

        Assert.Contains(result.Graph.Edges, e => e.SourceEntityId == "service:ERPWorker" && e.Type == DependencyEdgeType.Runs);
        Assert.Contains(result.Graph.Edges, e => e.SourceEntityId.Contains("Nightly") && e.Type == DependencyEdgeType.Runs);
    }

    [Fact]
    public void Correlate_ErpScenario_ComReferencesNativeDll()
    {
        var (result, _) = BuildAndCorrelate();

        Assert.Contains(result.Graph.Edges, e =>
            e.SourceEntityId.Contains("TEST-GUID") && e.TargetEntityId.Contains("VendorNative.dll") && e.Type == DependencyEdgeType.References);
    }

    [Fact]
    public void Correlate_ErpScenario_SiteBindsToCertificate()
    {
        var (result, _) = BuildAndCorrelate();

        Assert.Contains(result.Graph.Edges, e => e.Type == DependencyEdgeType.Binds && e.TargetEntityId.Contains("TEST"));
    }

    [Fact]
    public void Correlate_ErpScenario_ConfigurationReferencesDotNetRuntime()
    {
        var (result, _) = BuildAndCorrelate();

        Assert.Contains(result.Graph.Edges, e =>
            e.Type == DependencyEdgeType.References && e.TargetEntityId.StartsWith("runtime:DotNetRuntime"));
    }

    [Fact]
    public void Correlate_ErpScenario_NoEdgesToNonexistentDatabaseEndpointOrUncNodes()
    {
        // Database/Endpoint/UNC references have no corresponding entities yet — the graph must
        // never invent nodes or point edges at ids that don't resolve to a discovered entity.
        var (result, entities) = BuildAndCorrelate();

        foreach (var edge in result.Graph.Edges)
        {
            Assert.True(entities.ContainsKey(edge.SourceEntityId));
            Assert.True(entities.ContainsKey(edge.TargetEntityId));
        }
    }
}
