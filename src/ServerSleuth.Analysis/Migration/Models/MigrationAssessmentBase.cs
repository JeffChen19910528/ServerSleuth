using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Migration.Models;

/// <summary>
/// Common fields shared by <see cref="ApplicationMigrationAssessment"/> and
/// <see cref="ServerMigrationAssessment"/> — see skill.md (Phase 8A) §3, §7-8. No numeric score
/// anywhere: <see cref="OverallStatus"/> plus explicit counts/lists is the entire model.
/// </summary>
public abstract record MigrationAssessmentBase
{
    /// <summary>The worst <see cref="MigrationStatusImpact"/> among <c>Issues</c>, translated to
    /// a <see cref="Models.MigrationStatus"/> — see skill.md §2/§6's escalation table. A single
    /// Blocking issue always makes this Blocked; it can never be averaged or diluted away.</summary>
    public required MigrationStatus OverallStatus { get; init; }

    public required int BlockingIssueCount { get; init; }
    public required int RemediationIssueCount { get; init; }
    public required int ConditionalDependencyCount { get; init; }
    public required int InformationalIssueCount { get; init; }
    public required int UnclassifiedIssueCount { get; init; }

    public required int AffectedBoundaryCount { get; init; }
    public required int AffectedEntityCount { get; init; }

    /// <summary>Every MigrationIssue in this scope — ordinal-sorted by <c>IssueId</c>.</summary>
    public required IReadOnlyList<MigrationIssue> Issues { get; init; }

    /// <summary>Every MigrationDependency in this scope — ordinal-sorted by <c>DependencyId</c>.
    /// A dependency counts toward <see cref="ConditionalDependencyCount"/> only when it also has
    /// a Conditional-impact <see cref="MigrationIssue"/> backing it — a dependency with no
    /// RiskFinding at all is never counted as an "issue."</summary>
    public required IReadOnlyList<MigrationDependency> Dependencies { get; init; }

    /// <summary>Union of every Issue's and Dependency's own evidence in this scope — never
    /// fabricated, purely a rollup of what's already attached to each.</summary>
    public required IReadOnlyList<EvidenceRecord> Evidence { get; init; }
}
