using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk.Aggregation;

/// <summary>
/// Runs the Phase 7A ERP risk fixture (<see cref="ErpRiskFixtureTests"/>, skill.md Phase 7A §30)
/// through <see cref="RiskAggregator"/> and asserts on the application/server summaries it
/// actually produces — see skill.md (Phase 7B) §19: "The exact result must be derived from
/// actual rule outputs. Do NOT hard-code the expected severity if the existing rule output says
/// otherwise."
///
/// Observed (via a one-off exploratory probe against the real pipeline, since removed): the
/// fixture's ApplicationBoundaryEngine output produces five boundaries — 'ERP' (the IIS
/// application: site+app+web.config+ERP.Web.dll+Healthy.dll), 'ERPWorker', and one boundary per
/// BatchA/BatchB/BatchC scheduled-task/service anchor (the shared host.exe is deliberately NOT
/// merged into one boundary across them — skill.md Phase 5B). Of the 8 RiskFindings the fixture
/// produces:
///   - 'ERP' gets 3 findings — RR3-AccessDenied and RR4-MissingRuntime via their explicit
///     ApplicationBoundaryId, plus RR2-MissingBinary (the missing native import) via its
///     dependent ERP.Web.dll's boundary membership (SourceEntityId is the missing DLL itself,
///     which is never anyone's boundary member — RiskAggregator also unions each finding's
///     RelatedEntityIds, and RR2's RelatedEntityIds includes the dependent that DOES resolve).
///     All three are High → ApplicationRiskSummary.OverallSeverity == High, matching skill.md
///     (Phase 7B) §19's "ERP Web: HIGH" example exactly.
///   - 'ERPWorker' gets 1 finding (the deduplicated Critical missing-executable finding, whose
///     SourceEntityId is the worker .exe — a member of the ERPWorker boundary) →
///     OverallSeverity == Critical. This matches skill.md §19's "ERP Worker: CRITICAL" example
///     exactly.
///   - 'BatchA', 'BatchB', AND 'BatchC' each get the SAME RR10-SharedInfrastructure (Medium)
///     finding — see the "Shared Infrastructure Attribution Hardening" corrective task: a
///     dependency shared by three-or-more workloads legitimately affects every one of their
///     boundaries at once, and RiskAggregator now attributes it to all three deterministically
///     (via <c>RiskAnalysisContext.BoundaryIdsByEntityId</c>, ordinal-sorted) rather than
///     whichever boundary a dictionary enumeration happened to visit first. The finding is still
///     counted exactly ONCE at server level (<c>ServerRiskSummary.TotalFindingCount</c>) — only
///     its ApplicationRiskSummary attribution fans out, not the underlying logical finding.
///   - 3 findings (certificate expiry, external file share, external SQL dependency) resolve to
///     no boundary at all and are correctly server-scoped.
/// </summary>
public class ErpRiskAggregationFixtureTests
{
    private static (RiskAggregationResult Aggregation, RiskAnalysisResultFixture Result) BuildAndAggregate()
    {
        var site = EntityFactory.Site("ERP", @"D:\ERP\Web");
        var pool = EntityFactory.ApplicationPool("ERPAppPool");
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP\Web", poolId: pool.Id, siteId: site.Id);

        var webDll = EntityFactory.Dll(@"D:\ERP\Web\ERP.Web.dll", referencedBy: [app.Id], importsCsv: "VendorImport.dll");
        var missingImportDll = EntityFactory.Dll(@"D:\ERP\Web\VendorImport.dll", notFound: true);

        var appConfig = EntityFactory.Configuration(@"D:\ERP\Web\web.config", ownerEntityId: app.Id,
            dependencyReferences: ["RuntimeVersion: net8.0"]);
        appConfig.SetMetadata("ParseStatus", "AccessDenied");
        appConfig.SetMetadata("Database0.Type", "SqlServer");
        appConfig.SetMetadata("Database0.Host", "DB01");
        appConfig.SetMetadata("Database0.Port", "1433");
        appConfig.SetMetadata("Database0.Name", "ERP");
        appConfig.SetMetadata("NetworkPath0.Server", "FILESERVER");
        appConfig.SetMetadata("NetworkPath0.Share", "ERPData");

        var runtime6 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "6.0.30");
        var runtime10 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");

        EntityFactory.SetBinding(site, 0, "EXPIRING123");
        var expiringCert = EntityFactory.Certificate("erp.example.com", "EXPIRING123", validTo: DateTimeOffset.UtcNow.AddDays(10));

        var service = EntityFactory.Service("ERPWorker", @"D:\ERP\Worker\ERPWorker.exe");
        var missingWorkerExe = EntityFactory.Dll(@"D:\ERP\Worker\ERPWorker.exe", notFound: true);

        var batchA = EntityFactory.Service("BatchA", @"D:\ERP\Shared\host.exe");
        var batchB = EntityFactory.Service("BatchB", @"D:\ERP\Shared\host.exe");
        var batchC = EntityFactory.ScheduledTask(@"\ERP\BatchC", @"D:\ERP\Shared\host.exe");
        var sharedHostExe = EntityFactory.Dll(@"D:\ERP\Shared\host.exe");

        var healthyDll = EntityFactory.Dll(@"D:\ERP\Web\Healthy.dll", referencedBy: [app.Id]);
        var healthyCert = EntityFactory.Certificate("unused.example.com", "HEALTHY999", validTo: DateTimeOffset.UtcNow.AddYears(2));

        var entities = new List<DiscoveryEntity>
        {
            site, pool, app,
            webDll, missingImportDll,
            appConfig,
            runtime6, runtime10,
            expiringCert,
            service, missingWorkerExe,
            batchA, batchB, batchC, sharedHostExe,
            healthyDll, healthyCert
        };

        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);

        return (aggregation, new RiskAnalysisResultFixture(result, context, app.Id, service.Id));
    }

    private sealed record RiskAnalysisResultFixture(RiskAnalysisResult Result, RiskAnalysisContext Context, string AppId, string ServiceId);

    [Fact]
    public void ErpWebApplicationBoundary_OverallSeverity_IsHigh()
    {
        var (aggregation, fixture) = BuildAndAggregate();

        var web = Assert.Single(aggregation.Server.ApplicationSummaries, s => s.ApplicationBoundaryId == "boundary:iis-application:ERP:/");

        Assert.Equal(AggregateSeverity.High, web.OverallSeverity);
        Assert.Equal(3, web.TotalFindingCount); // AccessDenied + MissingRuntime + MissingBinary (native import)
        Assert.Equal(3, web.HighCount);
    }

    [Fact]
    public void ErpWorkerApplicationBoundary_OverallSeverity_IsCritical()
    {
        var (aggregation, _) = BuildAndAggregate();

        var worker = Assert.Single(aggregation.Server.ApplicationSummaries, s => s.ApplicationBoundaryId == "boundary:service:ERPWorker");

        Assert.Equal(AggregateSeverity.Critical, worker.OverallSeverity);
        Assert.Equal(1, worker.TotalFindingCount);
        Assert.Equal(1, worker.CriticalCount);
    }

    [Fact]
    public void SharedHostExeSharedInfrastructureFinding_AttributedToAllThreeBatchBoundaries()
    {
        // Corrective "Shared Infrastructure Attribution Hardening" behavior: a dependency
        // shared by 3+ workloads affects ALL of their (deliberately un-merged) boundaries, not
        // just whichever one a dictionary lookup happened to land on first.
        var (aggregation, _) = BuildAndAggregate();

        var batchBoundaries = new[] { "boundary:service:BatchA", "boundary:service:BatchB", "boundary:scheduledtask:\\ERP\\BatchC" };
        var owning = aggregation.Server.ApplicationSummaries.Where(s => batchBoundaries.Contains(s.ApplicationBoundaryId)).ToList();

        Assert.Equal(3, owning.Count);
        Assert.All(owning, summary =>
        {
            Assert.Equal(AggregateSeverity.Medium, summary.OverallSeverity);
            Assert.Equal(1, summary.SharedDependencyCount);
            Assert.Equal(1, summary.TotalFindingCount);
        });

        // Same logical finding — every boundary's copy shares the exact same Id.
        var findingIds = owning.SelectMany(s => s.Findings).Select(f => f.Id).Distinct().ToList();
        Assert.Single(findingIds);
    }

    [Fact]
    public void ServerSummary_OverallSeverity_IsCritical_AndCoversAllEightFindings()
    {
        var (aggregation, _) = BuildAndAggregate();

        Assert.Equal(AggregateSeverity.Critical, aggregation.Server.OverallSeverity);
        Assert.Equal(8, aggregation.Server.TotalFindingCount);
        Assert.Equal(1, aggregation.Server.CriticalCount);
        Assert.Equal(5, aggregation.Server.HighCount);
        Assert.Equal(2, aggregation.Server.MediumCount);
    }

    [Fact]
    public void ServerSummary_ThreeFindingsAreServerScoped_NeverDropped()
    {
        // Certificate expiry, external file share, external SQL dependency — the missing
        // native import (RR2) now resolves to the ERP boundary via its dependent's membership
        // (see the class-level doc comment), so only 3 of the 8 findings remain server-scoped.
        var (aggregation, _) = BuildAndAggregate();

        Assert.Equal(3, aggregation.Server.ServerScopedFindingCount);
        Assert.Equal(3, aggregation.Diagnostics.UnresolvedOwnershipCount);
        // Every server-scoped finding still appears in the whole-server Findings list.
        Assert.Equal(8, aggregation.Server.Findings.Count);
    }

    [Fact]
    public void ServerSummary_ApplicationSummaryCount_MatchesBoundariesWithFindings()
    {
        var (aggregation, _) = BuildAndAggregate();

        // ERP, ERPWorker, and — post-hardening — ALL THREE of BatchA/BatchB/BatchC (the shared
        // host.exe finding now correctly fans out to every boundary it affects instead of just
        // one), for a total of 5. See skill.md (Phase 7B) §4: a boundary with zero findings
        // still never receives a summary.
        Assert.Equal(5, aggregation.Server.ApplicationSummaries.Count);
        Assert.Equal(5, aggregation.Server.AffectedBoundaryCount);
        Assert.Equal(5, aggregation.Diagnostics.ApplicationSummariesCreated);

        // The underlying logical finding count is still exactly 8 — fan-out only affects how
        // many ApplicationRiskSummary views reference the shared finding, never how many times
        // it's counted at server level.
        Assert.Equal(8, aggregation.Server.TotalFindingCount);
    }
}
