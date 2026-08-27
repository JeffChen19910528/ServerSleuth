namespace ServerSleuth.Analysis.Migration.Models;

/// <summary>
/// The explainable migration status a <c>MigrationAssessmentBase</c> carries — see skill.md
/// (Phase 8A) §2. Deliberately NOT a 0-100 score, a weighted formula, or a probabilistic
/// readiness percentage: only this four-value classification, always backed by explicit
/// <see cref="MigrationIssue"/> records a report can enumerate. Declared in ascending severity
/// order so ordinal comparison is the deterministic "worse status wins" escalation rule used
/// throughout Migration Assessment — never string-sort this.
/// </summary>
public enum MigrationStatus
{
    Ready,
    ReadyWithConditions,
    NeedsRemediation,
    Blocked
}
