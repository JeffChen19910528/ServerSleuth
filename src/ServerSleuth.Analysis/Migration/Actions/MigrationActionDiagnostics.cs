namespace ServerSleuth.Analysis.Migration.Actions;

/// <summary>
/// Auditable, deterministic record of one <see cref="MigrationActionPlanner"/> run — mirrors
/// <c>MigrationDiagnostics</c>'s philosophy (Phase 8A): nothing about action generation, or the
/// decision NOT to generate one, ever happens silently.
/// </summary>
public sealed class MigrationActionDiagnostics
{
    public int IssuesConsidered { get; private set; }
    public int ActionsCreated { get; private set; }

    /// <summary>Informational-impact issues never produce a remediation action (§6, §22 fixture 2)
    /// — noted for awareness via a check instead, see <c>MigrationVerificationPlanner</c>.</summary>
    public int SkippedInformationalIssues { get; private set; }

    /// <summary>Unclassified-impact issues, and any classified issue whose RuleId this planner has
    /// no action mapping for, never produce a fabricated action (§6's "do not invent a migration
    /// consequence" carried forward from Phase 8A §6).</summary>
    public int SkippedUnclassifiedIssues { get; private set; }

    public void RecordIssueConsidered() => IssuesConsidered++;
    public void RecordActionCreated() => ActionsCreated++;
    public void RecordSkippedInformational() => SkippedInformationalIssues++;
    public void RecordSkippedUnclassified() => SkippedUnclassifiedIssues++;
}
