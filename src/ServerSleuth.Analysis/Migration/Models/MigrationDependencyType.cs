namespace ServerSleuth.Analysis.Migration.Models;

/// <summary>Closed taxonomy for <see cref="MigrationDependency"/> — see skill.md (Phase 8A)
/// §10's example list. Deliberately small; never grown per-target-hostname.</summary>
public enum MigrationDependencyType
{
    Database,
    Redis,
    ExternalApi,
    Ldap,
    FileShare,
    Certificate,
    Runtime,
    SharedBinary,
    Other
}

/// <summary>When a <see cref="MigrationDependency"/>'s requirement is meant to be verified —
/// see skill.md (Phase 8A) §11. A conceptual category only: Phase 8A defines these labels but
/// implements no check, execution, or probe of any kind for either phase.</summary>
public enum MigrationVerificationPhase
{
    PreMigration,
    PostMigration
}
