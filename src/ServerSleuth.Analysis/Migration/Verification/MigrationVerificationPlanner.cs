using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Models;

namespace ServerSleuth.Analysis.Migration.Verification;

/// <summary>
/// Transforms a <see cref="ServerMigrationAssessment"/> plus the <see cref="MigrationAction"/>s
/// <see cref="MigrationActionPlanner"/> produced from it into pre- and post-migration
/// <see cref="MigrationVerificationCheck"/> lists — see skill.md (Phase 8B) §1, §9-14.
///
/// Every action gets a matching "confirm this was addressed" PreMigration check and a "confirm
/// this on the target environment" PostMigration check (§9-13), except
/// <see cref="MigrationActionType.ReviewGraphIntegrity"/> — a one-time structural investigation,
/// not a repeatable pre/post verification (§19's own framing: the graph either is or isn't
/// trustworthy, there is no "verify it again after migration" step). Every
/// <see cref="MigrationDependency"/> not already covered by an action's
/// <see cref="MigrationAction.RelatedDependencyIds"/> still receives its own PostMigration check
/// (§8: "a dependency may exist without a risk... the migration plan must preserve it").
/// Informational-impact issues receive an inventory-only PostMigration check, never an action
/// (§22 fixture 2).
///
/// Pure in-memory, never mutates its inputs (§25), never touches any external system (§14, §23-24).
/// </summary>
public static class MigrationVerificationPlanner
{
    public static (IReadOnlyList<MigrationVerificationCheck> PreMigrationChecks, IReadOnlyList<MigrationVerificationCheck> PostMigrationChecks, MigrationVerificationDiagnostics Diagnostics)
        Plan(ServerMigrationAssessment server, IReadOnlyList<MigrationAction> actions)
    {
        var diagnostics = new MigrationVerificationDiagnostics();
        var pre = new List<MigrationVerificationCheck>();
        var post = new List<MigrationVerificationCheck>();
        var consumedDependencyIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var action in actions)
        {
            foreach (var dependencyId in action.RelatedDependencyIds)
            {
                consumedDependencyIds.Add(dependencyId);
            }

            if (action.ActionType == MigrationActionType.ReviewGraphIntegrity)
            {
                continue;
            }

            var checkType = MapPostCheckType(action);

            pre.Add(new MigrationVerificationCheck
            {
                CheckId = MigrationVerificationCheck.ComputeId(MigrationVerificationPhase.PreMigration, checkType, action.ActionId),
                Title = $"Confirm readiness: {action.Title}",
                Description = $"Before migration, confirm the following has been addressed: {action.Description}",
                Phase = MigrationVerificationPhase.PreMigration,
                CheckType = checkType,
                AffectedBoundaryIds = action.AffectedBoundaryIds,
                RelatedActionIds = [action.ActionId],
                RelatedDependencyIds = action.RelatedDependencyIds,
                Evidence = action.Evidence,
                Rationale = action.Rationale
            });
            diagnostics.RecordPreMigrationCheckCreated();

            post.Add(new MigrationVerificationCheck
            {
                CheckId = MigrationVerificationCheck.ComputeId(MigrationVerificationPhase.PostMigration, checkType, action.ActionId),
                Title = $"Verify on target: {action.Title}",
                Description = BuildPostDescription(checkType, action.Title),
                Phase = MigrationVerificationPhase.PostMigration,
                CheckType = checkType,
                AffectedBoundaryIds = action.AffectedBoundaryIds,
                RelatedActionIds = [action.ActionId],
                RelatedDependencyIds = action.RelatedDependencyIds,
                Evidence = action.Evidence,
                Rationale = action.Rationale
            });
            diagnostics.RecordPostMigrationCheckCreated();
        }

        foreach (var dependency in server.Dependencies)
        {
            if (consumedDependencyIds.Contains(dependency.DependencyId))
            {
                continue;
            }

            var checkType = MapDependencyCheckType(dependency.Type);
            post.Add(new MigrationVerificationCheck
            {
                CheckId = MigrationVerificationCheck.ComputeId(MigrationVerificationPhase.PostMigration, checkType, dependency.DependencyId),
                Title = $"Verify dependency: {dependency.Target}",
                Description = dependency.VerificationRequirement,
                Phase = MigrationVerificationPhase.PostMigration,
                CheckType = checkType,
                AffectedBoundaryIds = dependency.AffectedBoundaryIds,
                RelatedActionIds = [],
                RelatedDependencyIds = [dependency.DependencyId],
                Evidence = dependency.Evidence,
                Rationale = "Dependency identified by discovery evidence; no associated migration risk finding exists, but target-environment verification is still required before this migration can be considered complete."
            });
            diagnostics.RecordOrphanDependencyCheckCreated();
            diagnostics.RecordPostMigrationCheckCreated();
        }

        foreach (var issue in server.Issues)
        {
            if (issue.MigrationStatusImpact != MigrationStatusImpact.Informational)
            {
                continue;
            }

            post.Add(new MigrationVerificationCheck
            {
                CheckId = MigrationVerificationCheck.ComputeId(MigrationVerificationPhase.PostMigration, MigrationActionType.VerifyConfiguration, issue.IssueId),
                Title = $"Inventory note: {issue.Title}",
                Description = "Retained for awareness only; no migration action was required for this finding. Confirm this reference remains accurate in the target environment.",
                Phase = MigrationVerificationPhase.PostMigration,
                CheckType = MigrationActionType.VerifyConfiguration,
                AffectedBoundaryIds = issue.AffectedBoundaryIds,
                RelatedActionIds = [],
                RelatedDependencyIds = [],
                Evidence = issue.Evidence,
                Rationale = issue.PolicyDecisionReason
            });
            diagnostics.RecordInformationalCheckCreated();
            diagnostics.RecordPostMigrationCheckCreated();
        }

        return (
            pre.OrderBy(c => c.CheckId, StringComparer.Ordinal).ToList(),
            post.OrderBy(c => c.CheckId, StringComparer.Ordinal).ToList(),
            diagnostics);
    }

    /// <summary>
    /// The post-migration counterpart of a PreMigration action — §9-13. For a missing-binary
    /// action, refined by the affected boundary's own kind (parsed from its Id prefix — the same
    /// convention every scanner already uses: <c>boundary:service:...</c>/<c>boundary:scheduledtask:...</c>/
    /// <c>boundary:iis-application:...</c>) so "verify the service exists" (§10) is produced when
    /// the evidence actually supports it, falling back to a plain file-presence check otherwise.
    /// </summary>
    private static MigrationActionType MapPostCheckType(MigrationAction action) => action.ActionType switch
    {
        MigrationActionType.PrepareMissingBinary => RefineByBoundary(action.AffectedBoundaryIds),
        MigrationActionType.PrepareNativeDependency => MigrationActionType.VerifyNativeDependency,
        MigrationActionType.PrepareRuntime => MigrationActionType.VerifyRuntime,
        MigrationActionType.PrepareCertificate => MigrationActionType.VerifyCertificate,
        MigrationActionType.PrepareConfiguration => MigrationActionType.VerifyConfiguration,
        MigrationActionType.ReviewAccessDenied => MigrationActionType.VerifyConfiguration,
        MigrationActionType.VerifyExternalDependency => MigrationActionType.VerifyExternalDependency,
        MigrationActionType.DocumentDependency => MigrationActionType.VerifyFile,
        _ => MigrationActionType.VerifyConfiguration
    };

    private static MigrationActionType RefineByBoundary(IReadOnlyList<string> boundaryIds)
    {
        if (boundaryIds.Any(b => b.StartsWith("boundary:service:", StringComparison.Ordinal)))
        {
            return MigrationActionType.VerifyService;
        }

        if (boundaryIds.Any(b => b.StartsWith("boundary:scheduledtask:", StringComparison.Ordinal)))
        {
            return MigrationActionType.VerifyScheduledTask;
        }

        if (boundaryIds.Any(b => b.StartsWith("boundary:iis-application:", StringComparison.Ordinal)))
        {
            return MigrationActionType.VerifyIISApplication;
        }

        return MigrationActionType.VerifyFile;
    }

    private static MigrationActionType MapDependencyCheckType(MigrationDependencyType type) => type switch
    {
        MigrationDependencyType.Runtime => MigrationActionType.VerifyRuntime,
        MigrationDependencyType.Certificate => MigrationActionType.VerifyCertificate,
        MigrationDependencyType.SharedBinary => MigrationActionType.VerifyFile,
        _ => MigrationActionType.VerifyExternalDependency
    };

    private static string BuildPostDescription(MigrationActionType checkType, string actionTitle) => checkType switch
    {
        MigrationActionType.VerifyService => $"On the target environment, confirm the service exists, its executable path resolves, and its configuration matches what was migrated ({actionTitle}).",
        MigrationActionType.VerifyScheduledTask => $"On the target environment, confirm the scheduled task exists, is enabled, and its action/executable path resolves ({actionTitle}).",
        MigrationActionType.VerifyIISApplication => $"On the target environment, confirm the IIS site, application, application pool, and binding (including any certificate binding) exist as expected ({actionTitle}).",
        MigrationActionType.VerifyFile => $"On the target environment, confirm the required file exists and is reachable ({actionTitle}).",
        MigrationActionType.VerifyNativeDependency => $"On the target environment, confirm the required native library dependency resolves ({actionTitle}).",
        MigrationActionType.VerifyRuntime => $"On the target environment, confirm the required runtime family/version is installed ({actionTitle}).",
        MigrationActionType.VerifyCertificate => $"On the target environment, confirm the certificate is installed with the expected thumbprint and that any IIS binding references it correctly ({actionTitle}).",
        MigrationActionType.VerifyExternalDependency => $"On the target environment, confirm connectivity/configuration to the external dependency ({actionTitle}).",
        _ => $"On the target environment, confirm the configuration reference remains present and correctly resolved ({actionTitle}).",
    };
}
