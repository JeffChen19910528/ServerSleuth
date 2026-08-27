using ServerSleuth.Analysis.Migration.Diagnostics;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Risk.Models;

namespace ServerSleuth.Analysis.Migration.Assessment;

/// <summary>
/// Turns one RiskFinding plus the <see cref="MigrationPolicy"/> decision for it into one
/// <see cref="MigrationIssue"/> — see skill.md (Phase 8A) §4-5, §13-14, §16. Deliberately the
/// only place a <c>MigrationIssue</c> is constructed, so the traceability/confidence/evidence
/// invariants only need to be maintained in one spot.
/// </summary>
internal static class MigrationPolicyEvaluator
{
    public static MigrationIssue Evaluate(RiskFinding finding, IReadOnlyList<string> affectedBoundaryIds, MigrationPolicy policy, MigrationDiagnostics diagnostics)
    {
        diagnostics.RecordFindingEvaluated();
        var decision = policy.Classify(finding);

        if (decision.Impact == MigrationStatusImpact.Unclassified)
        {
            diagnostics.RecordUnclassifiedImpact(finding.RuleId, finding.Id, decision.Reason);
        }

        var affectedEntityIds = new[] { finding.SourceEntityId }
            .Concat(finding.RelatedEntityIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        diagnostics.RecordIssueCreated();

        return new MigrationIssue
        {
            IssueId = MigrationIssue.ComputeId(finding.Id),
            Title = finding.Title,
            Description = finding.Description,
            Severity = finding.Severity,
            MigrationStatusImpact = decision.Impact,
            RuleId = finding.RuleId,
            SourceRiskFindingId = finding.Id,
            AffectedBoundaryIds = affectedBoundaryIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
            AffectedEntityIds = affectedEntityIds,
            // Never fabricated, never amplified — the exact same evidence/confidence the source
            // RiskFinding already carries (skill.md §13-14).
            Evidence = finding.Evidence,
            Confidence = finding.Confidence,
            RequiredAction = decision.RequiredAction,
            PolicyDecisionReason = decision.Reason
        };
    }
}
