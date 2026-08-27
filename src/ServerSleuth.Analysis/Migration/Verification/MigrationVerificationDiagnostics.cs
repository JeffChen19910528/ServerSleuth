namespace ServerSleuth.Analysis.Migration.Verification;

/// <summary>
/// Auditable, deterministic record of one <see cref="MigrationVerificationPlanner"/> run — mirrors
/// <c>MigrationActionDiagnostics</c>'s philosophy.
/// </summary>
public sealed class MigrationVerificationDiagnostics
{
    public int PreMigrationChecksCreated { get; private set; }
    public int PostMigrationChecksCreated { get; private set; }

    /// <summary>Post-migration checks generated directly from a <c>MigrationDependency</c> that
    /// has no associated <c>MigrationAction</c> — e.g. a dependency with no RiskFinding backing it
    /// at all (§8), or one whose Issue was Informational/Unclassified. Counted separately so it's
    /// visible that the plan preserved these on purpose, not by accident.</summary>
    public int OrphanDependencyChecksCreated { get; private set; }

    /// <summary>Inventory-only checks generated for Informational-impact issues (§22 fixture 2) —
    /// never a remediation action, only awareness.</summary>
    public int InformationalChecksCreated { get; private set; }

    public void RecordPreMigrationCheckCreated() => PreMigrationChecksCreated++;
    public void RecordPostMigrationCheckCreated() => PostMigrationChecksCreated++;
    public void RecordOrphanDependencyCheckCreated() => OrphanDependencyChecksCreated++;
    public void RecordInformationalCheckCreated() => InformationalChecksCreated++;
}
