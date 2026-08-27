using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Risk.Models;

namespace ServerSleuth.Analysis.Migration.Actions;

/// <summary>
/// Transforms Phase 8A's <see cref="ServerMigrationAssessment"/> into a deterministic,
/// deduplicated list of <see cref="MigrationAction"/>s — see skill.md (Phase 8B) §1, §6.
///
/// Pure in-memory consumer of already-produced Phase 8A output: never re-runs discovery, never
/// re-evaluates Risk/Migration policy, never touches the filesystem/registry/process/network APIs
/// (§2, §24). Never mutates its input (§25) — reads <c>ServerMigrationAssessment.Issues</c>/
/// <c>.Dependencies</c> only, and returns entirely new records.
///
/// Operates on <c>ServerMigrationAssessment</c> rather than the per-application assessments
/// because it already covers every Issue/Dependency in the whole scan (application-scoped,
/// server-scoped, and shared-infrastructure alike — see <c>MigrationAssessmentEngine</c>'s own
/// doc comment), so no separate per-application pass is needed to avoid missing or duplicating
/// anything.
/// </summary>
public static class MigrationActionPlanner
{
    public static (IReadOnlyList<MigrationAction> Actions, MigrationActionDiagnostics Diagnostics) Plan(ServerMigrationAssessment server)
    {
        var diagnostics = new MigrationActionDiagnostics();

        // Index once — O(D) — instead of filtering server.Dependencies per issue, which would be
        // O(Issues * Dependencies) and blow the §26 performance budget at 10,000 x 5,000 scale.
        var dependenciesByFindingId = new Dictionary<string, List<MigrationDependency>>(StringComparer.Ordinal);
        foreach (var dependency in server.Dependencies)
        {
            if (dependency.RelatedRiskFindingId is null)
            {
                continue;
            }

            if (!dependenciesByFindingId.TryGetValue(dependency.RelatedRiskFindingId, out var list))
            {
                dependenciesByFindingId[dependency.RelatedRiskFindingId] = list = [];
            }

            list.Add(dependency);
        }

        var actions = new List<MigrationAction>();

        // server.Issues is already ordinal-sorted by IssueId (MigrationAssessmentCalculator), so
        // iterating it in order is itself deterministic; the explicit sort below is defense in
        // depth, not load-bearing.
        foreach (var issue in server.Issues)
        {
            diagnostics.RecordIssueConsidered();

            if (issue.MigrationStatusImpact == MigrationStatusImpact.Informational)
            {
                diagnostics.RecordSkippedInformational();
                continue;
            }

            var actionType = MapActionType(issue.RuleId);
            if (actionType is null)
            {
                // Covers both MigrationStatusImpact.Unclassified and any classified impact whose
                // RuleId this planner has no mapping for — recorded, never guessed (§6).
                diagnostics.RecordSkippedUnclassified();
                continue;
            }

            var relatedDependencyIds = dependenciesByFindingId.TryGetValue(issue.SourceRiskFindingId, out var deps)
                ? deps.Select(d => d.DependencyId).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList()
                : [];

            actions.Add(new MigrationAction
            {
                ActionId = MigrationAction.ComputeId(actionType.Value, issue.IssueId),
                ActionType = actionType.Value,
                Title = issue.Title,
                Description = issue.RequiredAction,
                Priority = MapPriority(issue.Severity),
                Phase = MigrationVerificationPhase.PreMigration,
                AffectedBoundaryIds = issue.AffectedBoundaryIds,
                AffectedEntityIds = issue.AffectedEntityIds,
                RelatedIssueIds = [issue.IssueId],
                RelatedDependencyIds = relatedDependencyIds,
                Evidence = issue.Evidence,
                Rationale = issue.PolicyDecisionReason
            });
            diagnostics.RecordActionCreated();
        }

        return (actions.OrderBy(a => a.ActionId, StringComparer.Ordinal).ToList(), diagnostics);
    }

    /// <summary>
    /// RuleId -> ActionType, keyed the same way <c>MigrationPolicy</c> keys RiskFinding -> Impact
    /// (§6): explicit, closed, testable. A RuleId with no entry here (including every RuleId the
    /// upstream <c>MigrationPolicy</c> itself has no entry for) yields <c>null</c> — never a
    /// guessed action (§6, §23).
    /// </summary>
    private static MigrationActionType? MapActionType(string ruleId) => ruleId switch
    {
        "RR1-MissingDependency" => MigrationActionType.PrepareNativeDependency,

        // RR2/RR6/RR7/RR8 all describe the same underlying requirement — "a binary this workload
        // depends on is missing or unresolved" — and Phase 7A's MissingBinaryEntityId merge
        // anchor already collapses the RR2/RR6/RR7/RR8 findings that describe the literal same
        // file into one RiskFinding/MigrationIssue before this planner ever runs, so mapping all
        // four to the same ActionType is what keeps the identity rule in MigrationAction.ComputeId
        // (§17) collapsing them into one action rather than accidentally creating near-duplicates.
        "RR2-MissingBinary" => MigrationActionType.PrepareMissingBinary,
        "RR6-ServiceDependency" => MigrationActionType.PrepareMissingBinary,
        "RR7-ScheduledTaskDependency" => MigrationActionType.PrepareMissingBinary,
        "RR8-ComDependency" => MigrationActionType.PrepareMissingBinary,

        "RR3-AccessDenied" => MigrationActionType.ReviewAccessDenied,
        "RR4-MissingRuntime" => MigrationActionType.PrepareRuntime,
        "RR5-CertificateExpiry" => MigrationActionType.PrepareCertificate,
        "RR9-ExternalDependency" => MigrationActionType.VerifyExternalDependency,
        "RR10-SharedInfrastructure" => MigrationActionType.DocumentDependency,

        // Only the High-severity (Conditional-impact) case of RR11 reaches this planner at all —
        // Low/Info severity RR11 findings are Informational-impact and are filtered out above.
        "RR11-ConfigurationRisk" => MigrationActionType.PrepareConfiguration,

        "RR12-GraphIntegrity" => MigrationActionType.ReviewGraphIntegrity,

        _ => null
    };

    private static MigrationActionPriority MapPriority(RiskSeverity severity) => severity switch
    {
        RiskSeverity.Critical => MigrationActionPriority.Critical,
        RiskSeverity.High => MigrationActionPriority.High,
        RiskSeverity.Medium => MigrationActionPriority.Medium,
        RiskSeverity.Low => MigrationActionPriority.Low,
        _ => MigrationActionPriority.Informational
    };
}
