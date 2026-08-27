using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Correlation.Boundaries;

public class ApplicationBoundaryEngineTests
{
    private static (List<DiscoveryEntity> Entities, DependencyGraph Graph) BuildErpFixture()
    {
        var site = EntityFactory.Site("ERP", @"D:\ERP\Web");
        var pool = EntityFactory.ApplicationPool("ERPAppPool");
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP\Web", poolId: pool.Id, siteId: site.Id);
        var config = EntityFactory.Configuration(@"D:\ERP\Web\web.config", ownerEntityId: app.Id);
        var webDll = EntityFactory.Dll(@"D:\ERP\Web\ERP.Web.dll", referencedBy: [app.Id]);

        var service = EntityFactory.Service("ERPWorker", @"D:\ERP\Worker\ERPWorker.exe");
        var workerExe = EntityFactory.Dll(@"D:\ERP\Worker\ERPWorker.exe");
        var task = EntityFactory.ScheduledTask(@"\ERP\Nightly", @"D:\ERP\Worker\ERPWorker.exe");

        var commonDll = EntityFactory.Dll(@"D:\ERP\Common\Common.dll");

        var entities = new List<DiscoveryEntity>
        {
            site, pool, app, config, webDll, service, workerExe, task, commonDll
        };

        var correlation = new CorrelationEngine().Correlate(entities);
        return (entities, correlation.Graph);
    }

    [Fact]
    public void Analyze_IisApplicationRoot_ClaimsOwnConfigAndBinary()
    {
        var (entities, graph) = BuildErpFixture();
        var result = new ApplicationBoundaryEngine().Analyze(entities, graph);

        var webBoundary = Assert.Single(result.Boundaries, b => b.MemberEntityIds.Contains("iis-application:ERP:/"));
        Assert.Contains(webBoundary.MemberEntityIds, id => id.Contains("web.config"));
        Assert.Contains(webBoundary.MemberEntityIds, id => id.Contains("ERP.Web.dll"));
        Assert.Equal(ConfidenceBand.VeryHigh, webBoundary.Confidence.Band);
    }

    [Fact]
    public void Analyze_ServiceAndTaskShareExecutable_MergeIntoOneWorkerBoundary()
    {
        var (entities, graph) = BuildErpFixture();
        var result = new ApplicationBoundaryEngine().Analyze(entities, graph);

        var workerBoundary = Assert.Single(result.Boundaries, b => b.MemberEntityIds.Contains("service:ERPWorker"));
        Assert.Contains(workerBoundary.MemberEntityIds, id => id == "scheduledtask:\\ERP\\Nightly");
        Assert.Contains(workerBoundary.MemberEntityIds, id => id.Contains("ERPWorker.exe"));
        Assert.Equal(ConfidenceBand.High, workerBoundary.Confidence.Band);
        Assert.Equal(1, result.Diagnostics.MergedBoundaries);
    }

    [Fact]
    public void Analyze_WebAndWorkerBoundaries_NeverAutomaticallyMergeDespiteSharedNamePrefix()
    {
        var (entities, graph) = BuildErpFixture();
        var result = new ApplicationBoundaryEngine().Analyze(entities, graph);

        // Two separate boundaries — "ERP" (web) and "ERPWorker" — never combined into one,
        // even though they share a name prefix and a common D:\ERP ancestor.
        Assert.Equal(2, result.Boundaries.Count);
    }

    [Fact]
    public void Analyze_CommonDll_IsNotClaimedByEitherWorkload_AndReportedAsUnresolved()
    {
        var (entities, graph) = BuildErpFixture();
        var result = new ApplicationBoundaryEngine().Analyze(entities, graph);

        Assert.DoesNotContain(result.Boundaries, b => b.MemberEntityIds.Any(id => id.Contains("Common.dll")));
        Assert.Contains(result.Diagnostics.UnresolvedOwnership, u => u.EntityId.Contains("Common.dll"));
    }

    [Fact]
    public void Analyze_CommonParentDirectory_IsRecordedAsAmbiguousCandidate_NeverMerged()
    {
        var (entities, graph) = BuildErpFixture();
        var result = new ApplicationBoundaryEngine().Analyze(entities, graph);

        Assert.Contains(result.Diagnostics.AmbiguousCandidates, c => c.Reason.Contains("common parent", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, result.Boundaries.Count); // still separate
    }

    [Fact]
    public void Analyze_ThreeAnchorsShareOneExecutable_NoneAreMerged_RecordedAsSharedBinary()
    {
        var serviceA = EntityFactory.Service("SvcA", @"C:\Shared\host.exe");
        var serviceB = EntityFactory.Service("SvcB", @"C:\Shared\host.exe");
        var task = EntityFactory.ScheduledTask(@"\Shared\Job", @"C:\Shared\host.exe");
        var hostExe = EntityFactory.Dll(@"C:\Shared\host.exe");

        var entities = new List<DiscoveryEntity> { serviceA, serviceB, task, hostExe };
        var graph = new CorrelationEngine().Correlate(entities).Graph;

        var result = new ApplicationBoundaryEngine().Analyze(entities, graph);

        Assert.Equal(3, result.Boundaries.Count); // none merged
        Assert.Equal(0, result.Diagnostics.MergedBoundaries);
        Assert.Contains(result.Diagnostics.SharedBinaries, s => s.SharingAnchorIds.Count == 3);
    }

    [Fact]
    public void Analyze_GivenIdenticalInputTwice_ProducesDeterministicBoundaryIds()
    {
        var (entities, graph) = BuildErpFixture();
        var engine = new ApplicationBoundaryEngine();

        var result1 = engine.Analyze(entities, graph);
        var result2 = engine.Analyze(entities, graph);

        Assert.Equal(
            result1.Boundaries.Select(b => b.Id).OrderBy(x => x),
            result2.Boundaries.Select(b => b.Id).OrderBy(x => x));
    }

    [Fact]
    public void Analyze_ServiceWithNoExecutablePath_ProducesNoAnchor_RecordedAsUnresolved()
    {
        var service = EntityFactory.Service("NoExe", null);
        var entities = new List<DiscoveryEntity> { service };
        var graph = new CorrelationEngine().Correlate(entities).Graph;

        var result = new ApplicationBoundaryEngine().Analyze(entities, graph);

        Assert.Empty(result.Boundaries);
        Assert.Contains(result.Diagnostics.UnresolvedOwnership, u => u.EntityId == service.Id);
    }

    [Fact]
    public void Analyze_MemberAppearingViaTwoSources_IsNotDuplicatedInMemberList()
    {
        // web.config is owned by the Application both via Phase 5A's Configures edge AND would
        // also match the direct OwnerEntityId scan — must appear exactly once.
        var (entities, graph) = BuildErpFixture();
        var result = new ApplicationBoundaryEngine().Analyze(entities, graph);

        var webBoundary = result.Boundaries.Single(b => b.MemberEntityIds.Contains("iis-application:ERP:/"));
        var configOccurrences = webBoundary.MemberEntityIds.Count(id => id.Contains("web.config"));
        Assert.Equal(1, configOccurrences);
    }

    [Fact]
    public void Analyze_MergedBoundaryEvidence_ContainsBothAnchorsEvidencePlusSharedTargetNote()
    {
        var (entities, graph) = BuildErpFixture();
        var result = new ApplicationBoundaryEngine().Analyze(entities, graph);

        var workerBoundary = result.Boundaries.Single(b => b.MemberEntityIds.Contains("service:ERPWorker"));
        Assert.Contains(workerBoundary.Evidence, e => e.Detail != null && e.Detail.Contains("Windows Service ImagePath"));
        Assert.Contains(workerBoundary.Evidence, e => e.Detail != null && e.Detail.Contains("Scheduled Task ExecAction"));
        Assert.Contains(workerBoundary.Evidence, e => e.Detail != null && e.Detail.Contains("Shared execution target"));
    }
}
