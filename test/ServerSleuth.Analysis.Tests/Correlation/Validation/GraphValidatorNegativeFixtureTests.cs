using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Correlation.Validation;

/// <summary>Re-validates the negative fixtures already established in Phase 5A/5B/5C — skill.md
/// (Phase 5D) §29. The validator must never turn any of these into a confirmed-dependency-style
/// error finding.</summary>
public class GraphValidatorNegativeFixtureTests
{
    private static GraphValidationResult Run(List<DiscoveryEntity> entities, out List<ApplicationBoundary> boundaries)
    {
        var graph = new CorrelationEngine().Correlate(entities).Graph;
        boundaries = new ApplicationBoundaryEngine().Analyze(entities, graph).Boundaries.ToList();
        var expansion = new DependencyExpansionEngine().Expand(entities, graph, boundaries);
        return new GraphValidator().Validate(entities, expansion, boundaries);
    }

    [Fact]
    public void Validate_200UnrelatedComRegistrations_ProducesNoErrorFindings()
    {
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP");
        var entities = new List<DiscoveryEntity> { app };

        for (var i = 0; i < 200; i++)
        {
            entities.Add(EntityFactory.Com($"{{UNRELATED-{i}}}", inprocServer32: $@"C:\Windows\System32\unrelated{i}.dll"));
            entities.Add(EntityFactory.Dll($@"C:\Windows\System32\unrelated{i}.dll"));
        }

        var result = Run(entities, out var boundaries);

        Assert.DoesNotContain(result.Findings, f => f.Severity == ValidationSeverity.Error);

        // Each COM legitimately references its own discovered Dll (so neither is an orphan),
        // but none of the 200 must ever end up owned by the unrelated "ERP" Application boundary.
        var appBoundary = boundaries.Single(b => b.MemberEntityIds.Contains(app.Id));
        Assert.DoesNotContain(appBoundary.MemberEntityIds, id => id.Contains("unrelated"));
    }

    [Fact]
    public void Validate_SharedParentDirectoryWorkloads_ProducesNoErrorFindings()
    {
        var erp = EntityFactory.Application("ERP", "/", @"D:\ERP\App");
        var erpWorkerService = EntityFactory.Service("ERPWorker", @"D:\ERP\WorkerApp\Worker.exe");
        var entities = new List<DiscoveryEntity> { erp, erpWorkerService };

        var result = Run(entities, out var boundaries);

        Assert.Equal(2, boundaries.Count);
        Assert.DoesNotContain(result.Findings, f => f.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Validate_ThreeWaySharedExecutable_ProducesNoErrorFindings()
    {
        var serviceA = EntityFactory.Service("SvcA", @"C:\Shared\host.exe");
        var serviceB = EntityFactory.Service("SvcB", @"C:\Shared\host.exe");
        var task = EntityFactory.ScheduledTask(@"\Shared\Job", @"C:\Shared\host.exe");
        var hostExe = EntityFactory.Dll(@"C:\Shared\host.exe");
        var entities = new List<DiscoveryEntity> { serviceA, serviceB, task, hostExe };

        var result = Run(entities, out var boundaries);

        Assert.Equal(3, boundaries.Count); // none merged
        Assert.DoesNotContain(result.Findings, f => f.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Validate_SameNamedDllsInSeparateAppRoots_ProducesNoErrorFindings()
    {
        var appA = EntityFactory.Application("AppA", "/", @"D:\AppA");
        var appB = EntityFactory.Application("AppB", "/", @"D:\AppB");
        var importerA = EntityFactory.Dll(@"D:\AppA\Importer.dll", importsCsv: "Vendor.dll");
        var vendorInA = EntityFactory.Dll(@"D:\AppA\Vendor.dll", referencedBy: [appA.Id]);
        var vendorInB = EntityFactory.Dll(@"D:\AppB\Vendor.dll", referencedBy: [appB.Id]);
        var entities = new List<DiscoveryEntity> { appA, appB, importerA, vendorInA, vendorInB };

        var result = Run(entities, out _);

        Assert.DoesNotContain(result.Findings, f => f.Severity == ValidationSeverity.Error);
        Assert.DoesNotContain(result.Findings, f => f.Code == "UnresolvedBinary"); // Vendor.dll DID resolve, correctly, to AppA's own copy
    }

    [Fact]
    public void Validate_MultipleRuntimeVersionsWithFamilyOnlyMarker_ProducesNoErrorFindings()
    {
        var runtime6 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "6.0.25");
        var runtime8 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "8.0.10");
        var runtime10 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP");
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", ownerEntityId: app.Id, dependencyReferences: ["Runtime: DotNet"]);
        var entities = new List<DiscoveryEntity> { runtime6, runtime8, runtime10, app, config };

        var result = Run(entities, out _);

        Assert.DoesNotContain(result.Findings, f => f.Severity == ValidationSeverity.Error);
        Assert.DoesNotContain(result.Findings, f => f.Code == "RuntimeMismatch");
    }

    [Fact]
    public void Validate_MissingBinaryReferencedByCom_ProducesMissingBinaryWarningNotError()
    {
        var com = EntityFactory.Com("{GUID}", inprocServer32: @"D:\Gone\Missing.dll");
        var missingDll = EntityFactory.Dll(@"D:\Gone\Missing.dll");
        missingDll.SetMetadata("FileStatus", "NotFound");
        var entities = new List<DiscoveryEntity> { com, missingDll };

        var result = Run(entities, out _);

        Assert.Contains(result.Findings, f => f.Code == "MissingBinary" && f.Severity == ValidationSeverity.Warning);
        Assert.DoesNotContain(result.Findings, f => f.Severity == ValidationSeverity.Error);
    }
}
