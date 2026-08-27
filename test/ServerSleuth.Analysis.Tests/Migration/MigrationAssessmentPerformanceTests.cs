using System.Diagnostics;
using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Boundaries.Diagnostics;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Diagnostics;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration;

/// <summary>
/// Synthetic-scale performance test — skill.md (Phase 8A) §20: >=10,000 RiskFindings, >=1,000
/// ApplicationBoundaries, >=5,000 dependency-worthy entities, >=50,000 affected entity/boundary
/// relationships. Builds a minimal-but-valid <see cref="RiskAnalysisContext"/> and
/// <see cref="RiskAggregationResult"/> directly (mirroring `RiskAggregationPerformanceTests`/
/// `SharedInfrastructureAttributionPerformanceTests`' own pattern) rather than running the full
/// Correlation pipeline. Entirely in-memory — no filesystem/scanner access.
/// </summary>
public class MigrationAssessmentPerformanceTests
{
    private const int BoundaryCount = 1_000;
    private const int FindingCount = 10_000;
    private const int RelatedIdsPerFinding = 5; // 10,000 * 5 = 50,000 related-entity references
    private const int ExternalDependencyCount = 5_000;

    [Fact]
    public void Assess_TenThousandFindings_OneThousandBoundaries_FiveThousandDependencies_CompletesUnderTenSeconds()
    {
        var boundaries = Enumerable.Range(0, BoundaryCount)
            .Select(i => new ApplicationBoundary
            {
                Id = $"boundary:{i}",
                Name = $"App{i}",
                MemberEntityIds = [],
                Evidence = [],
                Confidence = Confidence.High(),
                Reason = "synthetic migration performance fixture"
            })
            .ToList();

        var findings = new List<RiskFinding>(FindingCount);
        var ruleIds = new[] { "RR2-MissingBinary", "RR3-AccessDenied", "RR4-MissingRuntime", "RR9-ExternalDependency", "RR10-SharedInfrastructure" };
        var severities = new[] { RiskSeverity.Critical, RiskSeverity.High, RiskSeverity.Medium, RiskSeverity.Low, RiskSeverity.Info };

        for (var i = 0; i < FindingCount; i++)
        {
            var boundaryId = $"boundary:{i % BoundaryCount}";
            var relatedIds = Enumerable.Range(0, RelatedIdsPerFinding).Select(j => $"related:{i}:{j}").ToList();
            var ruleId = ruleIds[i % ruleIds.Length];
            var severity = severities[i % severities.Length];

            findings.Add(new RiskFinding
            {
                Id = RiskFinding.ComputeId(ruleId, $"entity:{i}", relatedIds),
                RuleId = ruleId,
                Category = RiskCategory.ExternalDependency,
                Severity = severity,
                Confidence = Confidence.High(),
                Title = $"Synthetic finding {i}",
                Description = "Synthetic migration performance fixture finding",
                SourceEntityId = $"entity:{i}",
                RelatedEntityIds = relatedIds,
                Evidence = [new EvidenceRecord { Type = ServerSleuth.Core.Enums.EvidenceType.ConfigurationFile, Location = $"entity:{i}" }],
                Recommendation = "N/A — synthetic performance fixture",
                ApplicationBoundaryId = boundaryId
            });
        }

        Assert.True(findings.Sum(f => f.RelatedEntityIds.Count) >= 50_000);

        var externalDependencies = Enumerable.Range(0, ExternalDependencyCount)
            .Select(i => new ExternalDependency
            {
                Id = $"externaldependency:{i}",
                Name = $"external-{i}.example.com",
                Type = "ExternalDependency",
                Source = "Configuration",
                Confidence = Confidence.Medium(),
                Kind = "Database",
                Endpoint = $"external-{i}.example.com:1433"
            })
            .ToList();

        var boundaryResult = new BoundaryAnalysisResult { Boundaries = boundaries, Diagnostics = new BoundaryDiagnostics() };
        var expansion = new DependencyExpansionResult
        {
            ExternalDependencies = externalDependencies,
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
        var aggregation = new RiskAggregator().Aggregate(context, analysisResult);

        var stopwatch = Stopwatch.StartNew();
        var migration = new MigrationAssessmentEngine().Assess(context, analysisResult, aggregation);
        stopwatch.Stop();

        Assert.Equal(FindingCount, migration.Server.Issues.Count);
        Assert.True(migration.Server.Dependencies.Count >= ExternalDependencyCount);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"MigrationAssessmentEngine.Assess took {stopwatch.Elapsed.TotalSeconds:0.00}s for {FindingCount} findings / {BoundaryCount} boundaries / {ExternalDependencyCount} dependencies — expected < 10s.");
    }
}
