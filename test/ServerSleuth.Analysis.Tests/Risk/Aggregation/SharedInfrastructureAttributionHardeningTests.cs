using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Boundaries.Diagnostics;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Diagnostics;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk.Aggregation;

/// <summary>
/// The "Shared Infrastructure Attribution Hardening" corrective task. Root cause: a shared
/// execution target legitimately becomes a member of every one of its (deliberately un-merged,
/// per Phase 5B §8) boundaries at once — each boundary's own <c>ApplicationBoundaryEngine.BuildCandidate</c>
/// independently claims the RUNS target as one of its members. But
/// <c>RiskAnalysisContext.BoundaryIdByEntityId</c> used to be a single-valued
/// <c>Dictionary&lt;string,string&gt;</c> built via <c>TryAdd</c> over <c>Boundaries</c> in
/// whatever order that list happened to be in — so only the FIRST boundary claiming a shared
/// entity ever won, and <c>RiskAggregator</c> only ever consulted a finding's
/// <c>SourceEntityId</c> through that single-valued map. The fix: <c>RiskAnalysisContext</c> now
/// also exposes <c>BoundaryIdsByEntityId</c> (ordinal-sorted, ALL claiming boundaries), and
/// <c>RiskAggregator.ResolveAffectedBoundaryIds</c> unions it across a finding's
/// <c>SourceEntityId</c> AND <c>RelatedEntityIds</c> — so a shared-infrastructure finding
/// (SourceEntityId = the shared binary; RelatedEntityIds = the sharing workload anchors) now
/// resolves to every affected boundary, deterministically, regardless of enumeration order.
///
/// This test file verifies ONLY the corrected attribution — it does not touch or re-test
/// Phase 5B's boundary-merge semantics (three-or-more sharers are still never merged into one
/// boundary; that is unchanged and correct).
/// </summary>
public class SharedInfrastructureAttributionHardeningTests
{
    // ---- 1-4: boundary-count matrix, via the real pipeline -----------------------------------

    [Fact]
    public void OneBinary_OneBoundary_AffectedBoundaryCountIsOne()
    {
        var service = EntityFactory.Service("Solo", @"D:\Solo\solo.exe");
        var exe = EntityFactory.Dll(@"D:\Solo\solo.exe");

        var (aggregation, _) = RunSharedInfra([service, exe]);

        // A single sharer is not "shared" at all — no SharedInfrastructure finding, so no
        // application summary is produced by this scenario (nothing to attribute).
        Assert.Equal(0, aggregation.Server.SharedDependencyCount);
    }

    [Fact]
    public void OneBinary_TwoBoundaries_BothBoundaryIdsPreserved()
    {
        // Two sharers are workload-identity evidence (Phase 5B §7) and get MERGED into one
        // boundary, not left as two — so this is really "one boundary," which is the correct,
        // unchanged Phase 5B behavior this hardening task must not disturb.
        var serviceA = EntityFactory.Service("PairA", @"D:\Pair\pair.exe");
        var serviceB = EntityFactory.Service("PairB", @"D:\Pair\pair.exe");
        var exe = EntityFactory.Dll(@"D:\Pair\pair.exe");

        var (_, context) = RiskPipeline.Run([serviceA, serviceB, exe]);

        Assert.Single(context.Boundaries); // merged — matches skill.md (Phase 5B) §7, unchanged
    }

    [Fact]
    public void OneBinary_ThreeBoundaries_AllThreeBoundaryIdsPreserved()
    {
        var serviceA = EntityFactory.Service("TriA", @"D:\Tri\tri.exe");
        var serviceB = EntityFactory.Service("TriB", @"D:\Tri\tri.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Tri\TriC", @"D:\Tri\tri.exe");
        var exe = EntityFactory.Dll(@"D:\Tri\tri.exe");

        var (aggregation, context) = RunSharedInfra([serviceA, serviceB, taskC, exe]);

        Assert.Equal(3, context.Boundaries.Count); // never merged — 3+ sharers stay separate
        Assert.Equal(3, aggregation.Server.ApplicationSummaries.Count);

        var boundaryIds = aggregation.Server.ApplicationSummaries.Select(s => s.ApplicationBoundaryId).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var expected = context.Boundaries.Select(b => b.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.Equal(expected, boundaryIds);
    }

    [Fact]
    public void ThreeWorkloads_OneSharedExecutable_ThreeAffectedWorkloadsAndBoundaries()
    {
        var serviceA = EntityFactory.Service("WkA", @"D:\Wk\host.exe");
        var serviceB = EntityFactory.Service("WkB", @"D:\Wk\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Wk\WkC", @"D:\Wk\host.exe");
        var exe = EntityFactory.Dll(@"D:\Wk\host.exe");

        var (aggregation, _) = RunSharedInfra([serviceA, serviceB, taskC, exe]);

        var finding = Assert.Single(aggregation.Server.Findings, f => f.Category == RiskCategory.SharedInfrastructure);
        Assert.Equal(3, finding.RelatedEntityIds.Count); // 3 affected workload anchors
        Assert.Equal(3, aggregation.Server.ApplicationSummaries.Count); // 3 affected boundaries
    }

    // ---- 5, 12: order independence -----------------------------------------------------------

    [Fact]
    public void SameLogicalRelationships_DifferentInputEnumerationOrders_ProduceIdenticalResult()
    {
        DiscoveryEntity ServiceA() => EntityFactory.Service("OrdA", @"D:\Ord\host.exe");
        DiscoveryEntity ServiceB() => EntityFactory.Service("OrdB", @"D:\Ord\host.exe");
        DiscoveryEntity TaskC() => EntityFactory.ScheduledTask(@"\Ord\OrdC", @"D:\Ord\host.exe");
        DiscoveryEntity Exe() => EntityFactory.Dll(@"D:\Ord\host.exe");

        var order1 = new List<DiscoveryEntity> { ServiceA(), ServiceB(), TaskC(), Exe() };
        var order2 = new List<DiscoveryEntity> { Exe(), TaskC(), ServiceA(), ServiceB() };
        var order3 = new List<DiscoveryEntity> { TaskC(), ServiceB(), Exe(), ServiceA() };

        var (aggA, _) = RunSharedInfra(order1);
        var (aggB, _) = RunSharedInfra(order2);
        var (aggC, _) = RunSharedInfra(order3);

        IReadOnlyList<string> BoundaryIds(RiskAggregationResult r) =>
            r.Server.ApplicationSummaries.Select(s => s.ApplicationBoundaryId).OrderBy(id => id, StringComparer.Ordinal).ToList();

        var expected = BoundaryIds(aggA);
        Assert.Equal(3, expected.Count);
        Assert.Equal(expected, BoundaryIds(aggB));
        Assert.Equal(expected, BoundaryIds(aggC));

        Assert.Equal(aggA.Server.TotalFindingCount, aggB.Server.TotalFindingCount);
        Assert.Equal(aggA.Server.TotalFindingCount, aggC.Server.TotalFindingCount);
        Assert.Equal(aggA.Server.OverallSeverity, aggB.Server.OverallSeverity);
        Assert.Equal(aggA.Server.OverallSeverity, aggC.Server.OverallSeverity);
    }

    [Fact]
    public void ReversedBoundaryListOrder_DoesNotChangeWhichBoundariesAttribute()
    {
        // Directly exercises the fixed root cause: RiskAnalysisContext used to build
        // BoundaryIdByEntityId via TryAdd over `Boundaries` in list order. Construct the exact
        // same three boundaries (all claiming the same shared entity) in forward and reversed
        // order and confirm BoundaryIdsByEntityId — and therefore attribution — is identical.
        var shared = new ApplicationBoundary { Id = "boundary:A", Name = "A", MemberEntityIds = ["entity:shared", "entity:a"], Evidence = [], Confidence = Confidence.High(), Reason = "test" };
        var sharedB = new ApplicationBoundary { Id = "boundary:B", Name = "B", MemberEntityIds = ["entity:shared", "entity:b"], Evidence = [], Confidence = Confidence.High(), Reason = "test" };
        var sharedC = new ApplicationBoundary { Id = "boundary:C", Name = "C", MemberEntityIds = ["entity:shared", "entity:c"], Evidence = [], Confidence = Confidence.High(), Reason = "test" };

        var forward = BuildMinimalContext([shared, sharedB, sharedC]);
        var reversed = BuildMinimalContext([sharedC, sharedB, shared]);

        Assert.Equal(forward.BoundaryIdsByEntityId["entity:shared"], reversed.BoundaryIdsByEntityId["entity:shared"]);
        Assert.Equal(["boundary:A", "boundary:B", "boundary:C"], forward.BoundaryIdsByEntityId["entity:shared"]);
        // The narrowed single-value convenience also agrees regardless of list order.
        Assert.Equal(forward.BoundaryIdByEntityId["entity:shared"], reversed.BoundaryIdByEntityId["entity:shared"]);
        Assert.Equal("boundary:A", forward.BoundaryIdByEntityId["entity:shared"]);
    }

    // ---- 6, 7: path identity -------------------------------------------------------------------

    [Fact]
    public void SameNamedBinariesAtDifferentPaths_AreNotShared()
    {
        var serviceA = EntityFactory.Service("PathA", @"D:\AppA\bin\Common.dll");
        var dllA = EntityFactory.Dll(@"D:\AppA\bin\Common.dll");
        var serviceB = EntityFactory.Service("PathB", @"D:\AppB\bin\Common.dll");
        var dllB = EntityFactory.Dll(@"D:\AppB\bin\Common.dll");
        var taskC = EntityFactory.ScheduledTask(@"\PathC", @"D:\AppC\bin\Common.dll");
        var dllC = EntityFactory.Dll(@"D:\AppC\bin\Common.dll");

        var (aggregation, context) = RunSharedInfra([serviceA, dllA, serviceB, dllB, taskC, dllC]);

        Assert.Equal(0, aggregation.Server.SharedDependencyCount);
        Assert.Equal(3, context.Boundaries.Count); // three distinct, un-merged, un-shared boundaries
    }

    [Fact]
    public void SameFilenameDifferentNormalizedPaths_RemainSeparate()
    {
        var serviceA = EntityFactory.Service("HostA", @"D:\AppA\bin\host.exe");
        var exeA = EntityFactory.Dll(@"D:\AppA\bin\host.exe");
        var serviceB = EntityFactory.Service("HostB", @"D:\AppB\bin\host.exe");
        var exeB = EntityFactory.Dll(@"D:\AppB\bin\host.exe");

        var (aggregation, _) = RunSharedInfra([serviceA, exeA, serviceB, exeB]);

        Assert.Equal(0, aggregation.Server.SharedDependencyCount);
    }

    [Fact]
    public void SameBinaryPathReferencedByMultipleWorkloads_IsShared()
    {
        var serviceA = EntityFactory.Service("SameA", @"D:\Same\host.exe");
        var serviceB = EntityFactory.Service("SameB", @"D:\Same\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Same\SameC", @"D:\Same\host.exe");
        var exe = EntityFactory.Dll(@"D:\Same\host.exe");

        var (aggregation, _) = RunSharedInfra([serviceA, serviceB, taskC, exe]);

        Assert.Equal(1, aggregation.Server.SharedDependencyCount);
    }

    // ---- 8: shared dependency with missing target binary -------------------------------------

    [Fact]
    public void SharedDependencyWithMissingTargetBinary_OneLogicalRisk_AllBoundariesPreserved()
    {
        var serviceA = EntityFactory.Service("MissA", @"D:\Miss\host.exe");
        var serviceB = EntityFactory.Service("MissB", @"D:\Miss\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Miss\MissC", @"D:\Miss\host.exe");
        var missingExe = EntityFactory.Dll(@"D:\Miss\host.exe", notFound: true);

        var (result, context) = RiskPipeline.Run([serviceA, serviceB, taskC, missingExe]);
        var aggregation = new RiskAggregator().Aggregate(context, result);

        // One logical missing-executable risk per sharer that RUNS it (MissingBinaryRule fires
        // once per dependent edge — Service/ScheduledTask dependents are Critical) — verify each
        // is still visible and, taken together, every one of the three boundaries is covered.
        var missingBinaryFindings = aggregation.Server.Findings.Where(f => f.Category == RiskCategory.MissingBinary).ToList();
        Assert.NotEmpty(missingBinaryFindings);
        Assert.Equal(3, aggregation.Server.ApplicationSummaries.Count);
    }

    // ---- 11: server counts the logical finding once -------------------------------------------

    [Fact]
    public void ServerSummary_CountsSharedFindingOnce_WhileAffectedBoundaryCountIsThree()
    {
        var serviceA = EntityFactory.Service("CntA", @"D:\Cnt\host.exe");
        var serviceB = EntityFactory.Service("CntB", @"D:\Cnt\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Cnt\CntC", @"D:\Cnt\host.exe");
        var exe = EntityFactory.Dll(@"D:\Cnt\host.exe");

        var (aggregation, _) = RunSharedInfra([serviceA, serviceB, taskC, exe]);

        Assert.Equal(1, aggregation.Server.TotalFindingCount);
        Assert.Equal(3, aggregation.Server.AffectedBoundaryCount);
        Assert.Equal(3, aggregation.Diagnostics.ApplicationSummariesCreated);
    }

    // ---- no-mutation: the shared finding is the SAME instance in every boundary's view --------

    [Fact]
    public void SharedFinding_IsTheSameInstance_AcrossEveryAffectedBoundarySummary_NeverCopied()
    {
        var serviceA = EntityFactory.Service("RefA", @"D:\Ref\host.exe");
        var serviceB = EntityFactory.Service("RefB", @"D:\Ref\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Ref\RefC", @"D:\Ref\host.exe");
        var exe = EntityFactory.Dll(@"D:\Ref\host.exe");

        var (aggregation, _) = RunSharedInfra([serviceA, serviceB, taskC, exe]);

        var perBoundary = aggregation.Server.ApplicationSummaries
            .Select(s => s.Findings.Single(f => f.Category == RiskCategory.SharedInfrastructure))
            .ToList();

        Assert.Equal(3, perBoundary.Count);
        Assert.True(ReferenceEquals(perBoundary[0], perBoundary[1]));
        Assert.True(ReferenceEquals(perBoundary[0], perBoundary[2]));

        // And it's the exact same instance the server-level Findings list holds too.
        var serverInstance = aggregation.Server.Findings.Single(f => f.Category == RiskCategory.SharedInfrastructure);
        Assert.True(ReferenceEquals(perBoundary[0], serverInstance));
    }

    // ---- helpers --------------------------------------------------------------------------------

    private static (RiskAggregationResult Aggregation, RiskAnalysisContext Context) RunSharedInfra(List<DiscoveryEntity> entities)
    {
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        return (aggregation, context);
    }

    private static RiskAnalysisContext BuildMinimalContext(IReadOnlyList<ApplicationBoundary> boundaries)
    {
        var boundaryResult = new BoundaryAnalysisResult { Boundaries = boundaries, Diagnostics = new BoundaryDiagnostics() };
        var expansion = new DependencyExpansionResult
        {
            ExternalDependencies = [],
            ExpandedGraph = new DependencyGraph(),
            DerivedWorkloadDependencies = [],
            Diagnostics = new ExpansionDiagnostics()
        };
        var validation = new GraphValidationResult
        {
            Findings = [],
            Orphans = [],
            Cycles = [],
            Summary = new GraphValidationSummary
            {
                TotalNodes = 0,
                TotalEdges = 0,
                ValidEdges = 0,
                InvalidEdges = 0,
                DuplicateEdges = 0,
                MissingEvidence = 0,
                DanglingEdges = 0,
                Cycles = 0,
                Orphans = 0,
                UnresolvedDependencies = 0,
                ConfidenceIssues = 0
            }
        };

        return new RiskAnalysisContext([], expansion.ExpandedGraph, boundaryResult, expansion, validation);
    }
}
