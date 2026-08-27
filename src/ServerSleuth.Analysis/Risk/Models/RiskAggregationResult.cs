using ServerSleuth.Analysis.Risk.Diagnostics;

namespace ServerSleuth.Analysis.Risk.Models;

/// <summary>The deterministic output of one <see cref="Aggregation.RiskAggregator"/> run —
/// mirrors <see cref="RiskAnalysisResult"/>'s shape. See skill.md (Phase 7B) §3, §16.</summary>
public sealed record RiskAggregationResult
{
    public required ServerRiskSummary Server { get; init; }
    public required RiskAggregationDiagnostics Diagnostics { get; init; }
}
