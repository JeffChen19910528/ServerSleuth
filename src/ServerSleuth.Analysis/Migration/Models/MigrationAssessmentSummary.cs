using ServerSleuth.Analysis.Migration.Diagnostics;

namespace ServerSleuth.Analysis.Migration.Models;

/// <summary>The deterministic output of one <c>MigrationAssessmentEngine</c> run — mirrors
/// <c>RiskAggregationResult</c>'s shape. See skill.md (Phase 8A) §3, §7.</summary>
public sealed record MigrationAssessmentSummary
{
    public required ServerMigrationAssessment Server { get; init; }
    public required MigrationDiagnostics Diagnostics { get; init; }
}
