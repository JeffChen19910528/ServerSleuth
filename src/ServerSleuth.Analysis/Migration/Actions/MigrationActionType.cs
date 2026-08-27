namespace ServerSleuth.Analysis.Migration.Actions;

/// <summary>
/// Closed, evidence-grounded taxonomy of migration action/verification "kinds" — see skill.md
/// (Phase 8B) §5. Shared by <see cref="MigrationAction.ActionType"/> (what must be prepared or
/// reviewed before migration) and <see cref="ServerSleuth.Analysis.Migration.Verification.MigrationVerificationCheck.CheckType"/>
/// (what must be verified before/after migration) rather than two near-duplicate enums — §5's own
/// example list already names several "Verify*" entries as action types, and §9-13 ask for the
/// exact same "Verify*" vocabulary on checks, so one closed set describes both "the kind of thing"
/// consistently. Deliberately does not include anything an action/check cannot back with existing
/// evidence — never invent a capability the tool doesn't actually have signal for (§5, §23).
/// </summary>
public enum MigrationActionType
{
    /// <summary>RR2/RR6/RR7/RR8: a binary a workload depends on (its own executable, a native
    /// import, a COM server, a scheduled task target) is confirmed missing on disk.</summary>
    PrepareMissingBinary,

    /// <summary>RR1: an import-table entry never resolved to any discovered binary at all.</summary>
    PrepareNativeDependency,

    /// <summary>RR4: the workload explicitly requires a runtime version that is not installed.</summary>
    PrepareRuntime,

    /// <summary>RR5: a certificate requires renewal/replacement (expired or expiring).</summary>
    PrepareCertificate,

    /// <summary>RR11 (High): an explicit network file-share/storage path configuration reference.</summary>
    PrepareConfiguration,

    /// <summary>A binary/file target's presence must be confirmed — used both for RR10's shared
    /// executable and as the post-migration counterpart of <see cref="DocumentDependency"/>.</summary>
    VerifyFile,

    /// <summary>Confirm a required runtime family/version resolves on the target environment.</summary>
    VerifyRuntime,

    /// <summary>Confirm a certificate is installed on the target with the expected thumbprint/binding.</summary>
    VerifyCertificate,

    /// <summary>Confirm a configuration file/reference remains present and correctly resolved.</summary>
    VerifyConfiguration,

    /// <summary>RR9: confirm the target environment can reach/authenticate to an external
    /// dependency (database, Redis, HTTP API, LDAP, file share).</summary>
    VerifyExternalDependency,

    /// <summary>Confirm a migrated Windows Service / systemd unit exists and is configured as expected.</summary>
    VerifyService,

    /// <summary>Confirm a migrated scheduled task exists, is enabled, and its action path resolves.</summary>
    VerifyScheduledTask,

    /// <summary>Confirm a migrated IIS site/application/pool/binding exists as expected.</summary>
    VerifyIISApplication,

    /// <summary>Confirm required native library dependencies resolve on the target.</summary>
    VerifyNativeDependency,

    /// <summary>RR10: no remediation is needed (the dependency already resolves) — only tracking
    /// that a shared dependency must be migrated once and remain reachable by every dependent.</summary>
    DocumentDependency,

    /// <summary>RR3: configuration could not be fully inspected — access must be obtained before
    /// migration completeness can be confirmed.</summary>
    ReviewAccessDenied,

    /// <summary>RR12: a dependency-graph structural integrity error must be investigated and
    /// resolved before this migration assessment can be considered reliable — a one-time manual
    /// review, never a repeatable pre/post verification (see
    /// <see cref="ServerSleuth.Analysis.Migration.Verification.MigrationVerificationPlanner"/>).</summary>
    ReviewGraphIntegrity
}
