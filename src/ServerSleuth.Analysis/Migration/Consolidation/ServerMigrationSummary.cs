using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Risk.Models;

namespace ServerSleuth.Analysis.Migration.Consolidation;

/// <summary>
/// The whole-server rollup exposed by <see cref="ServerMigrationAssessmentReport"/> — see
/// skill.md (Phase 8C) §4. Every field is either copied verbatim from an already-computed Phase
/// 7B/8A/8B value or a simple count over this phase's own consolidated
/// <see cref="ApplicationMigrationSummary"/> list — never a re-derived or re-weighted score.
/// Deliberately has no numeric migration-readiness value anywhere (§4: "Do not calculate a new
/// numeric migration score").
/// </summary>
public sealed record ServerMigrationSummary
{
    /// <summary>Copied verbatim from <c>MigrationAssessmentSummary.Server.OverallStatus</c> (Phase
    /// 8A) — never recomputed here.</summary>
    public required MigrationStatus OverallMigrationStatus { get; init; }

    /// <summary>Copied verbatim from <c>RiskAggregationResult.Server.OverallSeverity</c> (Phase
    /// 7B) — never recomputed here.</summary>
    public required AggregateSeverity OverallRiskSeverity { get; init; }

    public required int ApplicationCount { get; init; }
    public required int BlockedApplicationCount { get; init; }
    public required int NeedsRemediationApplicationCount { get; init; }
    public required int ReadyWithConditionsApplicationCount { get; init; }
    public required int ReadyApplicationCount { get; init; }

    /// <summary>Copied verbatim from <c>MigrationAssessmentSummary.Server</c> (Phase 8A) —
    /// never recomputed here.</summary>
    public required int BlockingIssueCount { get; init; }
    public required int RemediationIssueCount { get; init; }
    public required int ConditionalDependencyCount { get; init; }

    /// <summary>From <c>MigrationPlan</c> (Phase 8B) — a plain count, never re-generated.</summary>
    public required int ActionCount { get; init; }

    /// <summary><c>MigrationPlan.PreMigrationChecks.Count + PostMigrationChecks.Count</c>.</summary>
    public required int VerificationCheckCount { get; init; }

    public required int DependencyCount { get; init; }

    /// <summary>Copied verbatim from <c>MigrationAssessmentSummary.Server</c> (Phase 8A).</summary>
    public required int AffectedEntityCount { get; init; }
    public required int AffectedBoundaryCount { get; init; }
}
