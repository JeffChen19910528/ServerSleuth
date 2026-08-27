using System.Diagnostics;
using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Boundaries.Diagnostics;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Diagnostics;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk.Aggregation;

/// <summary>
/// Shared Infrastructure Attribution Hardening §16: >=10,000 boundaries, >=5,000 shared-
/// dependency findings, >=50,000 total entity/boundary relationships. Verifies the corrected
/// multi-boundary attribution (<see cref="RiskAnalysisContext.BoundaryIdsByEntityId"/>,
/// <see cref="RiskAggregator.ResolveAffectedBoundaryIds"/>) stays indexed/linear rather than
/// degrading into O(N²) boundary matching now that a finding can fan out to many boundaries.
/// Entirely in-memory — no filesystem/scanner access.
/// </summary>
public class SharedInfrastructureAttributionPerformanceTests
{
    private const int BoundaryCount = 10_000;
    private const int SharedFindingCount = 5_000;
    private const int RelatedIdsPerFinding = 10; // 5,000 * 10 = 50,000 relationships

    [Fact]
    public void TenThousandBoundaries_FiveThousandSharedFindings_FiftyThousandRelationships_CompletesUnderTenSeconds()
    {
        // One workload entity per boundary — BoundaryIdsByEntityId must resolve each.
        var boundaries = new List<ApplicationBoundary>(BoundaryCount);
        for (var i = 0; i < BoundaryCount; i++)
        {
            boundaries.Add(new ApplicationBoundary
            {
                Id = $"boundary:{i}",
                Name = $"Workload{i}",
                MemberEntityIds = [$"workload:{i}"],
                Evidence = [],
                Confidence = Confidence.High(),
                Reason = "synthetic shared-infrastructure performance fixture"
            });
        }

        // Each shared-dependency finding's SourceEntityId is the shared binary (never a
        // boundary member itself, matching real SharedInfrastructureRule output) and its
        // RelatedEntityIds are 10 distinct workload anchors spread across 10 different
        // boundaries — exactly the union-across-RelatedEntityIds path the hardening added.
        var findings = new List<RiskFinding>(SharedFindingCount);
        for (var i = 0; i < SharedFindingCount; i++)
        {
            var relatedIds = Enumerable.Range(0, RelatedIdsPerFinding)
                .Select(j => $"workload:{(i * RelatedIdsPerFinding + j) % BoundaryCount}")
                .ToList();

            findings.Add(new RiskFinding
            {
                Id = RiskFinding.ComputeId("RR10-SharedInfrastructure", $"dll:shared:{i}", relatedIds),
                RuleId = "RR10-SharedInfrastructure",
                Category = RiskCategory.SharedInfrastructure,
                Severity = RiskSeverity.Medium,
                Confidence = Confidence.High(),
                Title = $"Synthetic shared dependency {i}",
                Description = "Synthetic shared-infrastructure performance fixture finding",
                SourceEntityId = $"dll:shared:{i}",
                RelatedEntityIds = relatedIds,
                Evidence = [new EvidenceRecord { Type = ServerSleuth.Core.Enums.EvidenceType.FileSystem, Location = $"dll:shared:{i}" }],
                Recommendation = "N/A — synthetic performance fixture"
            });
        }

        Assert.True(findings.Sum(f => f.RelatedEntityIds.Count) >= 50_000);

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

        var contextBuildStopwatch = Stopwatch.StartNew();
        var context = new RiskAnalysisContext([], expansion.ExpandedGraph, boundaryResult, expansion, validation);
        contextBuildStopwatch.Stop();

        var analysisResult = new RiskAnalysisResult { Findings = findings, Diagnostics = new RiskDiagnostics() };

        var aggregateStopwatch = Stopwatch.StartNew();
        var aggregation = new RiskAggregator().Aggregate(context, analysisResult);
        aggregateStopwatch.Stop();

        Assert.Equal(SharedFindingCount, aggregation.Server.TotalFindingCount); // logical findings counted once each
        Assert.True(aggregation.Server.ApplicationSummaries.Count > 0);
        Assert.True(aggregateStopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"RiskAggregator.Aggregate took {aggregateStopwatch.Elapsed.TotalSeconds:0.00}s for {SharedFindingCount} shared findings / {BoundaryCount} boundaries / 50,000 relationships — expected < 10s.");
        Assert.True(contextBuildStopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"RiskAnalysisContext construction (BoundaryIdsByEntityId indexing) took {contextBuildStopwatch.Elapsed.TotalSeconds:0.00}s — expected < 10s.");
    }
}
