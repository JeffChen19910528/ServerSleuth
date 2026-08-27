using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Engine;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Models;
using Xunit.Abstractions;

namespace ServerSleuth.Analysis.Tests.Diagnostics;

/// <summary>
/// Phase 10A-H — Real-Scale Analysis Performance Investigation. Diagnostic-only: every stage
/// call below is the exact existing public API (<see cref="CorrelationEngine.Correlate"/>,
/// <see cref="ApplicationBoundaryEngine.Analyze"/>, etc.) — nothing is reimplemented, reordered,
/// or given different inputs than production would supply. Timing is via a plain
/// <see cref="System.Diagnostics.Stopwatch"/> wrapper (<see cref="StageMeasurement"/>); no
/// telemetry, no network call, no data sent anywhere — output is written to the test's own
/// <see cref="ITestOutputHelper"/> and, for archival, a local scratch file.
///
/// A 60-second per-stage diagnostic observation timeout applies only inside this test (see
/// skill.md §10: "this is NOT a production CLI timeout" — no timeout of any kind was added to
/// Analysis itself). If a stage exceeds it, the elapsed time is recorded, the stage is marked
/// aborted, and the investigation for that scale stops there rather than waiting indefinitely.
/// </summary>
public class PipelineTimingInvestigationTests
{
    private static readonly TimeSpan StageTimeout = TimeSpan.FromSeconds(60);
    private readonly ITestOutputHelper _output;

    public PipelineTimingInvestigationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(35_000)]
    public void Investigate(int scale)
    {
        var timings = new List<StageTiming>();
        var lines = new List<string> { $"=== Real-Scale Analysis Performance Investigation — scale={scale} ===" };

        var entities = RealisticScaleFixtureBuilder.Build(scale);
        var entityTypeCounts = entities.GroupBy(e => e.GetType().Name).OrderByDescending(g => g.Count()).ToList();
        lines.Add($"Synthetic entities built: {entities.Count}");
        foreach (var group in entityTypeCounts)
        {
            lines.Add($"  {group.Key,-16} {group.Count()}");
        }

        // --- 1. Correlation ---
        var (correlation, correlationMs, correlationTimedOut) = StageMeasurement.Measure(
            () => new CorrelationEngine().Correlate(entities), StageTimeout);
        timings.Add(new StageTiming
        {
            StageName = "Correlation",
            DurationMilliseconds = correlationMs,
            TimedOut = correlationTimedOut,
            InputEntityCount = entities.Count,
            OutputEdgeCount = correlation?.Graph.Edges.Count
        });
        Report(lines, timings[^1]);

        if (correlationTimedOut || correlation is null)
        {
            WriteAll(lines, timings, scale);
            return;
        }

        var edgesByType = correlation.Graph.Edges.GroupBy(e => e.Type).OrderByDescending(g => g.Count()).ToList();
        lines.Add("Edges by type:");
        foreach (var g in edgesByType)
        {
            lines.Add($"  {g.Key,-14} {g.Count()}");
        }

        // --- 2. Application Boundary ---
        var (boundaryResult, boundaryMs, boundaryTimedOut) = StageMeasurement.Measure(
            () => new ApplicationBoundaryEngine().Analyze(entities, correlation.Graph), StageTimeout);
        timings.Add(new StageTiming
        {
            StageName = "Boundary",
            DurationMilliseconds = boundaryMs,
            TimedOut = boundaryTimedOut,
            InputEntityCount = entities.Count,
            InputEdgeCount = correlation.Graph.Edges.Count,
            BoundaryCount = boundaryResult?.Boundaries.Count
        });
        Report(lines, timings[^1]);

        if (boundaryTimedOut || boundaryResult is null)
        {
            WriteAll(lines, timings, scale);
            return;
        }

        // --- 3. Dependency Expansion ---
        var (expansion, expansionMs, expansionTimedOut) = StageMeasurement.Measure(
            () => new DependencyExpansionEngine().Expand(entities, correlation.Graph, boundaryResult.Boundaries), StageTimeout);
        timings.Add(new StageTiming
        {
            StageName = "Expansion",
            DurationMilliseconds = expansionMs,
            TimedOut = expansionTimedOut,
            InputEntityCount = entities.Count,
            InputEdgeCount = correlation.Graph.Edges.Count,
            OutputEdgeCount = expansion?.ExpandedGraph.Edges.Count,
            DependencyCount = expansion?.ExternalDependencies.Count
        });
        Report(lines, timings[^1]);

        if (expansionTimedOut || expansion is null)
        {
            WriteAll(lines, timings, scale);
            return;
        }

        // --- 4. Graph Validation ---
        var (validation, validationMs, validationTimedOut) = StageMeasurement.Measure(
            () => new GraphValidator().Validate(entities, expansion, boundaryResult.Boundaries), StageTimeout);
        timings.Add(new StageTiming
        {
            StageName = "Validation",
            DurationMilliseconds = validationMs,
            TimedOut = validationTimedOut,
            InputEdgeCount = expansion.ExpandedGraph.Edges.Count,
            FindingCount = validation?.Findings.Count
        });
        Report(lines, timings[^1]);

        if (validationTimedOut || validation is null)
        {
            WriteAll(lines, timings, scale);
            return;
        }

        // --- 5. Risk Analysis ---
        var (riskContext, riskContextMs, _) = StageMeasurement.Measure(
            () => new RiskAnalysisContext(entities, expansion.ExpandedGraph, boundaryResult, expansion, validation));
        lines.Add($"  (RiskAnalysisContext construction: {riskContextMs:0.0}ms)");

        var (riskResult, riskMs, riskTimedOut) = StageMeasurement.Measure(
            () => new RiskRuleEngine(RiskPipeline.AllRules).Analyze(riskContext!), StageTimeout);
        timings.Add(new StageTiming
        {
            StageName = "Risk Analysis",
            DurationMilliseconds = riskMs,
            TimedOut = riskTimedOut,
            FindingCount = riskResult?.Findings.Count
        });
        Report(lines, timings[^1]);

        if (riskTimedOut || riskResult is null)
        {
            WriteAll(lines, timings, scale);
            return;
        }

        // --- 6. Risk Aggregation ---
        var (aggregation, aggregationMs, aggregationTimedOut) = StageMeasurement.Measure(
            () => new RiskAggregator().Aggregate(riskContext!, riskResult), StageTimeout);
        timings.Add(new StageTiming { StageName = "Risk Aggregation", DurationMilliseconds = aggregationMs, TimedOut = aggregationTimedOut });
        Report(lines, timings[^1]);

        if (aggregationTimedOut || aggregation is null)
        {
            WriteAll(lines, timings, scale);
            return;
        }

        // --- 7. Migration Assessment ---
        var (assessment, assessmentMs, assessmentTimedOut) = StageMeasurement.Measure(
            () => new MigrationAssessmentEngine().Assess(riskContext!, riskResult, aggregation), StageTimeout);
        timings.Add(new StageTiming { StageName = "Migration Assessment", DurationMilliseconds = assessmentMs, TimedOut = assessmentTimedOut });
        Report(lines, timings[^1]);

        if (assessmentTimedOut || assessment is null)
        {
            WriteAll(lines, timings, scale);
            return;
        }

        // --- 8. Migration Planning ---
        var (plan, planMs, planTimedOut) = StageMeasurement.Measure(
            () => MigrationPlanEngine.Plan(assessment), StageTimeout);
        timings.Add(new StageTiming { StageName = "Migration Planning", DurationMilliseconds = planMs, TimedOut = planTimedOut });
        Report(lines, timings[^1]);

        if (planTimedOut || plan is null)
        {
            WriteAll(lines, timings, scale);
            return;
        }

        // --- 9. Consolidation ---
        var (report, reportMs, reportTimedOut) = StageMeasurement.Measure(
            () => ServerMigrationAssessmentReportEngine.Build(riskContext!, aggregation, assessment, plan), StageTimeout);
        timings.Add(new StageTiming { StageName = "Consolidation", DurationMilliseconds = reportMs, TimedOut = reportTimedOut });
        Report(lines, timings[^1]);

        WriteAll(lines, timings, scale);
    }

    private void Report(List<string> lines, StageTiming timing)
    {
        var line = timing.FormatRow();
        lines.Add(line);
        _output.WriteLine(line);
    }

    private void WriteAll(List<string> lines, List<StageTiming> timings, int scale)
    {
        lines.Add(string.Empty);
        lines.Add("Summary:");
        foreach (var t in timings)
        {
            lines.Add("  " + t.FormatRow());
        }

        foreach (var line in lines)
        {
            _output.WriteLine(line);
        }

        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "ServerSleuthDiagnostics");
            Directory.CreateDirectory(dir);
            System.IO.File.WriteAllLines(Path.Combine(dir, $"investigation-{scale}.txt"), lines);
        }
        catch (IOException)
        {
            // Best-effort archival only — the ITestOutputHelper output above is authoritative.
        }
    }
}
