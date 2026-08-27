using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Verification;
using ServerSleuth.Analysis.Risk.Models;

namespace ServerSleuth.Analysis.Migration.Consolidation;

/// <summary>
/// One application's consolidated migration picture — see skill.md (Phase 8C) §5. Wraps Phase
/// 8A's own <see cref="ApplicationMigrationAssessment"/> (BoundaryId, ApplicationName,
/// MigrationStatus, Issues, Dependencies, affected-entity counts — all reused verbatim, never
/// duplicated into new fields) and adds only what that model doesn't already carry: the
/// boundary's own risk severity (Phase 7B) and the Phase 8B actions/checks that affect it.
///
/// Every instance corresponds to an actual <see cref="ServerSleuth.Core.Boundaries.ApplicationBoundary"/>
/// that Phase 8A already included — this type never invents application ownership; it is built
/// by <see cref="ServerMigrationAssessmentReportEngine"/> purely by joining/filtering existing
/// Phase 7B/8A/8B output.
/// </summary>
public sealed record ApplicationMigrationSummary
{
    /// <summary>Phase 8A's own per-application assessment — BoundaryId, ApplicationName,
    /// MigrationStatus, Issues, Dependencies, affected-entity/boundary counts.</summary>
    public required ApplicationMigrationAssessment Assessment { get; init; }

    /// <summary>Copied verbatim from the matching <c>ApplicationRiskSummary.OverallSeverity</c>
    /// (Phase 7B) — never recomputed here.</summary>
    public required AggregateSeverity RiskSeverity { get; init; }

    /// <summary>Every <c>MigrationAction</c> (Phase 8B) whose <c>AffectedBoundaryIds</c> includes
    /// this application's boundary — a reference into <c>MigrationPlan.Actions</c>, never a copy
    /// or a re-generated action.</summary>
    public required IReadOnlyList<MigrationAction> Actions { get; init; }

    public required IReadOnlyList<MigrationVerificationCheck> PreMigrationChecks { get; init; }
    public required IReadOnlyList<MigrationVerificationCheck> PostMigrationChecks { get; init; }
}
