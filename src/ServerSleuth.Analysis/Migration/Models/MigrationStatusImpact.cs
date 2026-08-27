namespace ServerSleuth.Analysis.Migration.Models;

/// <summary>
/// What one <see cref="MigrationIssue"/> (i.e. one RiskFinding, reinterpreted through
/// <c>MigrationPolicy</c>) contributes to the overall <see cref="MigrationStatus"/> — the
/// explicit, testable, RuleId-driven classification skill.md (Phase 8A) §1 requires instead of
/// a blind <c>RiskSeverity → MigrationStatus</c> mapping. Ascending order matches
/// <see cref="MigrationStatus"/>'s own escalation ordering (Informational/Unclassified never
/// escalate the overall status; a single Blocking issue always wins).
/// </summary>
public enum MigrationStatusImpact
{
    /// <summary>Noted for awareness only — never escalates <see cref="MigrationStatus"/> away
    /// from <see cref="MigrationStatus.Ready"/> by itself.</summary>
    Informational,

    /// <summary>skill.md (Phase 8A) §6: "If the existing RiskFinding model does not provide
    /// enough semantic information to make a safe decision, record the issue as an explicit
    /// 'UnclassifiedMigrationImpact' diagnostic rather than inventing a migration consequence."
    /// Never escalates the overall status by itself — recorded so it is never silently lost,
    /// but not treated as a proven blocker either.</summary>
    Unclassified,

    /// <summary>Escalates the owning assessment to at least <see cref="MigrationStatus.ReadyWithConditions"/>.</summary>
    Conditional,

    /// <summary>Escalates the owning assessment to at least <see cref="MigrationStatus.NeedsRemediation"/>.</summary>
    RemediationRequired,

    /// <summary>Escalates the owning assessment to <see cref="MigrationStatus.Blocked"/>.</summary>
    Blocking
}
