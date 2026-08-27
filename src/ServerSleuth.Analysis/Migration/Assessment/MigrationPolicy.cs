using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Migration.Models;

namespace ServerSleuth.Analysis.Migration.Assessment;

public sealed record MigrationPolicyDecision
{
    public required MigrationStatusImpact Impact { get; init; }
    public required string Reason { get; init; }
    public required string RequiredAction { get; init; }
}

/// <summary>
/// The explicit, deterministic, independently-testable policy skill.md (Phase 8A) §1/§5 demand
/// instead of a blind <c>RiskSeverity → MigrationStatus</c> mapping. Classification is keyed
/// PRIMARILY by <c>RuleId</c> (i.e. rule semantics — what kind of problem this is), with the
/// finding's own <c>Severity</c> as a secondary refinement only for the handful of rules whose
/// severity genuinely varies per-finding (<c>MissingBinaryRule</c>, <c>CertificateExpiryRule</c>).
/// This is deliberate: skill.md §6 explicitly forbids "blindly classify every Critical finding
/// as Blocked" — e.g. an expired (Critical) certificate is remediable pre-migration, not a
/// structural blocker, so RR5 never escalates to Blocking regardless of severity.
///
/// Every entry below is documented with the actual RiskSeverity values the owning rule can
/// produce today (traced from `Risk/Rules/*.cs`, not assumed) so the table's behavior can be
/// verified rule-by-rule rather than taken on faith. An Info-severity finding (no current rule
/// produces one, but the policy stays correct if one ever does) is always Informational
/// regardless of RuleId. A RuleId this table has no entry for is <see cref="MigrationStatusImpact.Unclassified"/>
/// — recorded as a diagnostic, never guessed (skill.md §6's own explicit fallback instruction).
/// </summary>
public sealed class MigrationPolicy
{
    public MigrationPolicyDecision Classify(RiskFinding finding)
    {
        if (finding.Severity == RiskSeverity.Info)
        {
            return Informational("An Info-severity finding is by definition non-actionable — no RuleId-specific policy overrides this.");
        }

        return finding.RuleId switch
        {
            // RR1-MissingDependency: always High (never varies) — a binary's import table names
            // a library that never resolved to any discovered binary at all. Requires locating/
            // bundling the dependency before migration, but is not itself proof the *workload's
            // own* executable is missing — so RemediationRequired, not Blocking.
            "RR1-MissingDependency" => RemediationRequired(
                "Native/managed dependency did not resolve to any discovered binary — required for migration verification.",
                "Confirm the required library is available (and migrated) on the target environment, or bundle it alongside this binary."),

            // RR2-MissingBinary: Critical when the dependent is a Service/ScheduledTask (its own
            // runnable is confirmed absent — the workload cannot be reproduced at all without
            // it); High for any other dependent (an application-level binary dependency, still
            // requiring remediation but not proof the workload itself cannot start).
            "RR2-MissingBinary" when finding.Severity == RiskSeverity.Critical => Blocking(
                "A Service or Scheduled Task's own executable dependency is confirmed missing on disk — the workload cannot be reproduced on the target without it.",
                "Locate and migrate the missing executable, or rebuild it for the target environment, before this workload can be migrated."),
            "RR2-MissingBinary" => RemediationRequired(
                "A binary dependency is confirmed missing on disk.",
                "Locate and migrate the missing file, or rebuild it for the target environment, before migration."),

            // RR3-AccessDenied: always Medium. Means "could not verify," never "is broken" — see
            // AccessDeniedRule's own doc comment. Requires follow-up access before migration
            // completeness can be confirmed.
            "RR3-AccessDenied" => RemediationRequired(
                "Configuration could not be fully inspected — migration completeness cannot be confirmed from current evidence.",
                "Obtain the access needed to complete inspection of this configuration before relying on this migration assessment as complete."),

            // RR4-MissingRuntime: always High, and only ever fires when the workload explicitly
            // references a runtime version that is not installed (never inferred from generic
            // family markers — see MissingRuntimeRule).
            "RR4-MissingRuntime" => RemediationRequired(
                "The workload explicitly requires a runtime version that is not installed.",
                "Install the required runtime version on the target environment before migration."),

            // RR5-CertificateExpiry: Critical (expired) or High (expiring very soon) both need
            // renewal/replacement — a remediation task, never a structural blocker, so this
            // rule NEVER escalates to Blocking regardless of severity (skill.md §6's own
            // explicit "do not blindly classify every Critical finding as Blocked" example).
            // Medium (within the warning window) is a planning concern, not yet a defect.
            "RR5-CertificateExpiry" when finding.Severity is RiskSeverity.Critical or RiskSeverity.High => RemediationRequired(
                "Certificate requires renewal or replacement before or during migration.",
                "Renew or replace the certificate, and re-bind it on the target environment, before or immediately after cutover."),
            "RR5-CertificateExpiry" => Conditional(
                "Certificate is nearing its expiry window.",
                "Verify the certificate's replacement timeline as part of migration planning."),

            // RR6-ServiceDependency: always Critical — a Windows Service's own executable is
            // confirmed missing. Always Blocking: the service cannot be reproduced on the
            // target at all without it.
            "RR6-ServiceDependency" => Blocking(
                "A Windows Service's own executable dependency is confirmed missing on disk — the service cannot be reproduced on the target without it.",
                "Locate and migrate the missing executable, or rebuild it for the target environment, before this service can be migrated."),

            // RR7-ScheduledTaskDependency: always High (deliberately lower than a Service's
            // Critical, per the rule's own doc comment) — remediation required, not a hard
            // blocker on its own.
            "RR7-ScheduledTaskDependency" => RemediationRequired(
                "A Scheduled Task's executable dependency is missing or unresolved.",
                "Confirm the scheduled task's executable will be present (or rebuilt) on the target environment before migration."),

            // RR8-ComDependency: always High — a COM registration references a binary that
            // never resolved. Requires verification/remediation before migration.
            "RR8-ComDependency" => RemediationRequired(
                "A COM registration references a binary that could not be resolved.",
                "Verify the COM server binary before migration, or plan to re-register the component on the target environment."),

            // RR9-ExternalDependency: always Medium — skill.md §6 explicitly lists every kind
            // of external dependency (database, Redis, external API, LDAP, file share) under
            // READY_WITH_CONDITIONS: connectivity/configuration verification on the target, not
            // a defect to remediate here.
            "RR9-ExternalDependency" => Conditional(
                "External dependency requires target-environment connectivity/configuration verification.",
                "Confirm the target environment can reach and authenticate to this external dependency after migration."),

            // RR10-SharedInfrastructure: always Medium — skill.md §6/§9: sharing affects impact/
            // scope, never severity. Must be migrated once and remain reachable by every
            // workload that depends on it.
            "RR10-SharedInfrastructure" => Conditional(
                "Shared execution target must be migrated once and remain reachable by every workload that depends on it.",
                "Ensure the shared binary is migrated once and remains reachable by every affected workload, rather than duplicated inconsistently."),

            // RR11-ConfigurationRisk: severity genuinely varies per-finding (traced from the
            // rule's own Markers table, not assumed) — FileShare/NetworkStorage references are
            // High ("an explicit path dependency," per the rule's own doc comment, and exactly
            // the UNC/NFS/CIFS migration-sensitivity skill.md §22 calls out); UnixSocket is Low;
            // EnvVar/Endpoint/Database are Info (deferring to ExternalDependencyRule for their
            // real severity). A blanket Informational mapping here would silently discard a
            // genuine High-severity file-share/network-storage dependency, contradicting §6's
            // own READY_WITH_CONDITIONS "configuration dependency" bucket — so High is
            // Conditional (verify the path resolves in the target environment), matching RR9's
            // treatment of the same kind of external-path dependency; Low stays Informational,
            // matching the rule's own "rarely migration-blocking" framing.
            "RR11-ConfigurationRisk" when finding.Severity == RiskSeverity.High => Conditional(
                "Configuration references an explicit network file-share or storage path dependency.",
                "Confirm this file-share/network-storage path resolves correctly in the target environment, or update it as part of migration."),
            "RR11-ConfigurationRisk" => Informational(
                "Migration-sensitive configuration reference noted for awareness."),

            // RR12-GraphIntegrity: always High (GraphIntegrityRule's own fixed severity), but by
            // construction only ever fires for an Error-severity GraphValidator finding (a
            // dangling edge, missing evidence, confidence-without-evidence, or similar hard
            // structural problem) — see skill.md §19: migration assessment must not proceed as
            // if the graph is trustworthy when such an error exists. Always Blocking regardless
            // of the RiskSeverity label, since the underlying validator finding is already
            // reserved for genuinely hard structural problems, never a mere warning.
            "RR12-GraphIntegrity" => Blocking(
                "Dependency graph integrity error — migration assessment cannot be considered reliable until this structural inconsistency is resolved.",
                "Investigate and resolve this structural inconsistency in the dependency graph before relying on this migration assessment."),

            _ => Unclassified(finding.RuleId)
        };
    }

    private static MigrationPolicyDecision Blocking(string reason, string action) =>
        new() { Impact = MigrationStatusImpact.Blocking, Reason = reason, RequiredAction = action };

    private static MigrationPolicyDecision RemediationRequired(string reason, string action) =>
        new() { Impact = MigrationStatusImpact.RemediationRequired, Reason = reason, RequiredAction = action };

    private static MigrationPolicyDecision Conditional(string reason, string action) =>
        new() { Impact = MigrationStatusImpact.Conditional, Reason = reason, RequiredAction = action };

    private static MigrationPolicyDecision Informational(string reason) =>
        new() { Impact = MigrationStatusImpact.Informational, Reason = reason, RequiredAction = "No action required for migration; retained for awareness only." };

    private static MigrationPolicyDecision Unclassified(string ruleId) =>
        new()
        {
            Impact = MigrationStatusImpact.Unclassified,
            Reason = $"No migration policy is defined for RuleId '{ruleId}' — recorded rather than guessed (skill.md Phase 8A §6).",
            RequiredAction = "Review this finding manually; its migration consequence has not been classified by policy."
        };
}
