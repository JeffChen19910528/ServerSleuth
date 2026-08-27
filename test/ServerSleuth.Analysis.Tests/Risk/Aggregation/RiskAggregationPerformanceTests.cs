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
/// Synthetic-scale performance test — see skill.md (Phase 7B) §18: >=10,000 RiskFinding
/// records, >=1,000 ApplicationBoundaries, >=50,000 total related-entity references. Builds a
/// minimal-but-valid <see cref="RiskAnalysisContext"/> directly (Aggregation only ever reads
/// <c>Boundaries</c>/<c>BoundaryIdByEntityId</c> from it, never <c>AllEntities</c>/<c>Graph</c>
/// content) rather than running the full Correlation pipeline, since that pipeline's own
/// performance is out of this phase's scope — entirely in-memory, no filesystem/scanner access.
/// </summary>
public class RiskAggregationPerformanceTests
{
    private const int BoundaryCount = 1_000;
    private const int FindingCount = 10_000;
    private const int RelatedIdsPerFinding = 5; // 10,000 * 5 = 50,000 total related-entity references

    [Fact]
    public void Aggregate_TenThousandFindings_OneThousandBoundaries_FiftyThousandRelatedRefs_CompletesUnderTenSeconds()
    {
        var boundaries = Enumerable.Range(0, BoundaryCount)
            .Select(i => new ApplicationBoundary
            {
                Id = $"boundary:{i}",
                Name = $"App{i}",
                MemberEntityIds = [],
                Evidence = [],
                Confidence = Confidence.High(),
                Reason = "synthetic performance fixture"
            })
            .ToList();

        var findings = new List<RiskFinding>(FindingCount);
        for (var i = 0; i < FindingCount; i++)
        {
            var boundaryId = $"boundary:{i % BoundaryCount}";
            var relatedIds = Enumerable.Range(0, RelatedIdsPerFinding).Select(j => $"related:{i}:{j}").ToList();

            findings.Add(new RiskFinding
            {
                Id = RiskFinding.ComputeId("PERF", $"entity:{i}", relatedIds),
                RuleId = "PERF",
                Category = (RiskCategory)(i % 12),
                Severity = (RiskSeverity)(i % 5),
                Confidence = Confidence.High(),
                Title = $"Synthetic finding {i}",
                Description = "Synthetic performance fixture finding",
                SourceEntityId = $"entity:{i}",
                RelatedEntityIds = relatedIds,
                Evidence = [new EvidenceRecord { Type = ServerSleuth.Core.Enums.EvidenceType.ConfigurationFile, Location = $"entity:{i}" }],
                Recommendation = "N/A — synthetic performance fixture",
                ApplicationBoundaryId = boundaryId
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

        var context = new RiskAnalysisContext([], expansion.ExpandedGraph, boundaryResult, expansion, validation);
        var analysisResult = new RiskAnalysisResult { Findings = findings, Diagnostics = new RiskDiagnostics() };

        var stopwatch = Stopwatch.StartNew();
        var aggregation = new RiskAggregator().Aggregate(context, analysisResult);
        stopwatch.Stop();

        Assert.Equal(FindingCount, aggregation.Server.TotalFindingCount);
        Assert.Equal(BoundaryCount, aggregation.Server.ApplicationSummaries.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"RiskAggregator.Aggregate took {stopwatch.Elapsed.TotalSeconds:0.00}s for {FindingCount} findings / {BoundaryCount} boundaries — expected < 10s.");
    }
}
