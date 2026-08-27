using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Correlation.Validation;

/// <summary>Runs the full Phase 5A → 5B → 5C → 5D pipeline over the established ERP fixture —
/// skill.md (Phase 5D) §28.</summary>
public class GraphValidatorErpFixtureTests
{
    private static GraphValidationResult RunFullPipeline(out List<ApplicationBoundary> boundaries)
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
        config.SetMetadata("NetworkPath0.Server", "FILESERVER");
        config.SetMetadata("NetworkPath0.Share", "ERPData");

        var service = EntityFactory.Service("ERPWorker", @"D:\ERP\Worker\ERPWorker.exe");
        var workerExe = EntityFactory.Dll(@"D:\ERP\Worker\ERPWorker.exe");
        var task = EntityFactory.ScheduledTask(@"\ERP\Nightly", @"D:\ERP\Worker\ERPWorker.exe");

        var entities = new List<DiscoveryEntity>
        {
            site, pool, app, webDll, vendorNativeDll, com, certificate, runtime8, runtime10, config, service, workerExe, task
        };

        var graph = new CorrelationEngine().Correlate(entities).Graph;
        boundaries = new ApplicationBoundaryEngine().Analyze(entities, graph).Boundaries.ToList();
        var expansion = new DependencyExpansionEngine().Expand(entities, graph, boundaries);

        return new GraphValidator().Validate(entities, expansion, boundaries);
    }

    [Fact]
    public void Validate_FullErpPipeline_ReportsNoDanglingEdges()
    {
        var result = RunFullPipeline(out _);

        Assert.DoesNotContain(result.Findings, f => f.Code is "DanglingSource" or "DanglingTarget");
    }

    [Fact]
    public void Validate_FullErpPipeline_ReportsNoMissingRequiredEvidence()
    {
        var result = RunFullPipeline(out _);

        Assert.DoesNotContain(result.Findings, f => f.Code == "MissingEvidence");
        Assert.DoesNotContain(result.Findings, f => f.Code == "MissingProvenanceEvidence");
    }

    [Fact]
    public void Validate_FullErpPipeline_ReportsNoConfidenceEscalation()
    {
        var result = RunFullPipeline(out _);

        Assert.DoesNotContain(result.Findings, f => f.Code == "ConfidenceEscalation");
        Assert.DoesNotContain(result.Findings, f => f.Code == "ConfidenceWithoutEvidence");
    }

    [Fact]
    public void Validate_FullErpPipeline_ReportsNoUnexpectedDuplicateNodesOrEdges()
    {
        var result = RunFullPipeline(out _);

        Assert.DoesNotContain(result.Findings, f => f.Code == "DuplicateNodeId");
        Assert.DoesNotContain(result.Findings, f => f.Code == "DuplicateEdge");
    }

    [Fact]
    public void Validate_FullErpPipeline_UnresolvedRuntime10_RemainsExplicitlyClassifiedNotAnError()
    {
        var result = RunFullPipeline(out _);

        // .NET 10 is installed but never referenced by anything — it must surface only as an
        // Expected orphan, never as an error-level finding.
        Assert.Contains(result.Orphans, o => o.EntityId.Contains("10.0.0") && o.Classification == OrphanClassification.Expected);
    }

    [Fact]
    public void Validate_FullErpPipeline_NoErrorSeverityFindingsAtAll()
    {
        var result = RunFullPipeline(out _);

        var errors = result.Findings.Where(f => f.Severity == ValidationSeverity.Error).ToList();
        Assert.Empty(errors);
    }
}
