using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Migration.Verification;

namespace ServerSleuth.Analysis.Migration.Consolidation;

/// <summary>
/// The final, platform-neutral, presentation-ready migration assessment view — see skill.md
/// (Phase 8C) §1, §3. Composes Phase 5B (<c>ApplicationBoundary</c>), 5C
/// (<c>DependencyExpansionResult</c>), 5D (<c>GraphValidationResult</c>), 7B
/// (<c>RiskSummary</c>/<c>ApplicationRiskSummary</c>/<c>ServerRiskSummary</c>), 8A
/// (<c>MigrationAssessment</c>), and 8B (<c>MigrationPlan</c>) into one
/// Server → Application → Dependency → Issue → Action → Verification structure.
///
/// This is consolidation only: every Issue/Dependency/Action/Check here is a reference into an
/// object Phase 7A-8B already produced, never a copy, never a re-derived value. See
/// <see cref="ServerMigrationAssessmentReportEngine"/> for the composition logic and its own
/// "no new analysis" guarantee.
/// </summary>
public sealed record ServerMigrationAssessmentReport
{
    /// <summary>The exact Phase 8A output this report was built from — full traceability back to
    /// the source, never discarded once consolidated.</summary>
    public required MigrationAssessmentSummary Assessment { get; init; }

    /// <summary>The exact Phase 8B output this report was built from.</summary>
    public required MigrationPlan Plan { get; init; }

    public required ServerMigrationSummary ServerSummary { get; init; }

    /// <summary>One entry per <c>ApplicationBoundary</c> Phase 8A already included (i.e. one with
    /// >=1 attributed finding) — ordinal-sorted by BoundaryId. Never a synthetic boundary.</summary>
    public required IReadOnlyList<ApplicationMigrationSummary> ApplicationAssessments { get; init; }

    /// <summary>Every <c>MigrationIssue</c> with zero <c>AffectedBoundaryIds</c> — platform-level
    /// AccessDenied, graph integrity, unresolved infrastructure, and similar findings that belong
    /// to no application (skill.md §15). Sorted Severity descending, then RuleId, then IssueId
    /// (ordinal) — see skill.md §16.</summary>
    public required IReadOnlyList<MigrationIssue> ServerLevelIssues { get; init; }

    /// <summary>Every <c>MigrationDependency</c> whose <c>AffectedBoundaryIds</c> spans more than
    /// one application — e.g. a shared executable RUN by three services (skill.md §6). A filtered
    /// view of <c>Assessment.Server.Dependencies</c>, never a duplicate/merged record: sharing is
    /// still exactly one logical dependency with multiple boundaries listed on it, exactly as
    /// Phase 8A already produced it.</summary>
    public required IReadOnlyList<MigrationDependency> SharedInfrastructure { get; init; }

    /// <summary>Every dependency, grouped deterministically by <c>MigrationDependencyType</c>
    /// (skill.md §7).</summary>
    public required IReadOnlyList<MigrationDependencyGroup> Dependencies { get; init; }

    /// <summary>Phase 8B's own <c>MigrationPlan.Actions</c>, re-sorted Priority descending then
    /// ActionId ordinal (skill.md §16) — the exact same action instances, never regenerated
    /// (skill.md §9: "Do not generate new actions in Phase 8C").</summary>
    public required IReadOnlyList<MigrationAction> Actions { get; init; }

    public required IReadOnlyList<MigrationVerificationCheck> PreMigrationChecks { get; init; }
    public required IReadOnlyList<MigrationVerificationCheck> PostMigrationChecks { get; init; }

    /// <summary>How complete the discovery evidence behind this assessment was — see
    /// <see cref="Consolidation.AssessmentCoverage"/>. Deliberately never influences
    /// <c>ServerSummary.OverallMigrationStatus</c> (skill.md §12).</summary>
    public required AssessmentCoverage Coverage { get; init; }

    public required IReadOnlyList<CoverageWarning> CoverageWarnings { get; init; }

    /// <summary>Every Error-severity <see cref="ValidationFinding"/> from Phase 5D's
    /// <c>GraphValidationResult</c>, passed through unmodified (skill.md §14) — GraphValidator is
    /// never re-run here. The existing Phase 8A policy (every such finding already flows through
    /// as an RR12-GraphIntegrity <c>RiskFinding</c>, always classified Blocking) remains the sole
    /// authority for whether these make the migration Blocked; this list exists purely so the
    /// underlying structural findings stay visible in the consolidated view, not to make a second,
    /// independent Blocked/Ready decision about them.</summary>
    public required IReadOnlyList<ValidationFinding> GraphValidationErrors { get; init; }

    public required ConsolidationDiagnostics Diagnostics { get; init; }
}
