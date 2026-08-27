using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Migration.Assessment;

/// <summary>
/// The single, shared implementation of every metric/status appearing on both
/// <see cref="ApplicationMigrationAssessment"/> and <see cref="ServerMigrationAssessment"/> —
/// mirrors <c>RiskSummaryCalculator</c>'s role for Phase 7B. See skill.md (Phase 8A) §2, §6-8.
/// </summary>
internal static class MigrationAssessmentCalculator
{
    /// <summary>Escalation policy, skill.md (Phase 8A) §2/§6: the worst
    /// <see cref="MigrationStatusImpact"/> present wins outright — a single Blocking issue can
    /// never be averaged or diluted away. Informational/Unclassified issues never escalate the
    /// status by themselves (skill.md §6's own "record rather than invent a consequence").</summary>
    public static MigrationStatus ComputeOverallStatus(IReadOnlyList<MigrationIssue> issues)
    {
        if (issues.Any(i => i.MigrationStatusImpact == MigrationStatusImpact.Blocking)) return MigrationStatus.Blocked;
        if (issues.Any(i => i.MigrationStatusImpact == MigrationStatusImpact.RemediationRequired)) return MigrationStatus.NeedsRemediation;
        if (issues.Any(i => i.MigrationStatusImpact == MigrationStatusImpact.Conditional)) return MigrationStatus.ReadyWithConditions;
        return MigrationStatus.Ready;
    }

    public static int CountByImpact(IReadOnlyList<MigrationIssue> issues, MigrationStatusImpact impact) =>
        issues.Count(i => i.MigrationStatusImpact == impact);

    public static int ComputeAffectedEntityCount(IReadOnlyList<MigrationIssue> issues) =>
        issues.SelectMany(i => i.AffectedEntityIds).Distinct(StringComparer.Ordinal).Count();

    public static IReadOnlyList<EvidenceRecord> RollupEvidence(IReadOnlyList<MigrationIssue> issues, IReadOnlyList<MigrationDependency> dependencies) =>
        issues.SelectMany(i => i.Evidence)
            .Concat(dependencies.SelectMany(d => d.Evidence))
            .GroupBy(e => (e.Type, e.Location, e.Detail))
            .Select(g => g.First())
            .ToList();

    public static IReadOnlyList<MigrationIssue> Sorted(IEnumerable<MigrationIssue> issues) =>
        issues.OrderBy(i => i.IssueId, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<MigrationDependency> Sorted(IEnumerable<MigrationDependency> dependencies) =>
        dependencies.OrderBy(d => d.DependencyId, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<ApplicationMigrationAssessment> Sorted(IEnumerable<ApplicationMigrationAssessment> assessments) =>
        assessments.OrderBy(a => a.ApplicationBoundaryId, StringComparer.Ordinal).ToList();
}
