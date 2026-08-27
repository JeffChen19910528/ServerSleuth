using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Risk.Aggregation;

/// <summary>
/// The single, shared implementation of every metric that appears on both
/// <see cref="ApplicationRiskSummary"/> and <see cref="ServerRiskSummary"/> — used by both
/// <see cref="ApplicationRiskAggregator"/> and <see cref="ServerRiskAggregator"/> so the two
/// summary levels can never silently diverge on how a count/metric is computed. See skill.md
/// (Phase 7B) §3, §6, §8-12.
/// </summary>
internal static class RiskSummaryCalculator
{
    /// <summary>How many findings <see cref="TopRisks"/> keeps — deliberately small and fixed
    /// so the field stays genuinely a "top risks" highlight, not a re-listing of every finding
    /// (that's what <c>Findings</c> is for). See skill.md (Phase 7B) §11.</summary>
    public const int TopRisksLimit = 10;

    public static AggregateSeverity ComputeOverallSeverity(IReadOnlyList<RiskFinding> findings)
    {
        // Escalation policy, skill.md (Phase 7B) §2: the highest severity present wins outright
        // — a single Critical finding can never be averaged away or otherwise hidden.
        if (findings.Count == 0) return AggregateSeverity.None;
        if (findings.Any(f => f.Severity == RiskSeverity.Critical)) return AggregateSeverity.Critical;
        if (findings.Any(f => f.Severity == RiskSeverity.High)) return AggregateSeverity.High;
        if (findings.Any(f => f.Severity == RiskSeverity.Medium)) return AggregateSeverity.Medium;
        if (findings.Any(f => f.Severity == RiskSeverity.Low)) return AggregateSeverity.Low;
        return AggregateSeverity.Info;
    }

    public static int CountBySeverity(IReadOnlyList<RiskFinding> findings, RiskSeverity severity) =>
        findings.Count(f => f.Severity == severity);

    public static int ComputeAffectedEntityCount(IReadOnlyList<RiskFinding> findings) =>
        findings
            .SelectMany(f => new[] { f.SourceEntityId }.Concat(f.RelatedEntityIds))
            .Distinct(StringComparer.Ordinal)
            .Count();

    public static IReadOnlyDictionary<RiskCategory, int> ComputeCategoryCounts(IReadOnlyList<RiskFinding> findings) =>
        findings
            .GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.Count());

    public static int ComputeSharedDependencyCount(IReadOnlyList<RiskFinding> findings) =>
        findings.Count(f => f.Category == RiskCategory.SharedInfrastructure);

    /// <summary>Highest single Confidence.Value among the contributing findings — see skill.md
    /// (Phase 7B) §10: never a sum/average that could push the aggregate above what any one
    /// piece of evidence individually supports. <c>Confidence(0.0)</c> when empty.</summary>
    public static Confidence ComputeAggregateConfidence(IReadOnlyList<RiskFinding> findings) =>
        findings.Count == 0 ? new Confidence(0.0) : new Confidence(findings.Max(f => f.Confidence.Value));

    /// <summary>Per-finding impact score used only for <see cref="ComputeTopRisks"/> ordering —
    /// the number of distinct entities the finding touches (its SourceEntityId plus every
    /// RelatedEntityIds entry). Purely derived from the finding's own explicit Ids, never from
    /// naming similarity. See skill.md (Phase 7B) §8, §11.</summary>
    private static int Impact(RiskFinding finding) =>
        new[] { finding.SourceEntityId }.Concat(finding.RelatedEntityIds).Distinct(StringComparer.Ordinal).Count();

    /// <summary>Ordering, skill.md (Phase 7B) §11: Severity desc, Impact desc, Confidence desc,
    /// RuleId ordinal, FindingId ordinal — then capped to <see cref="TopRisksLimit"/>.</summary>
    public static IReadOnlyList<RiskFinding> ComputeTopRisks(IReadOnlyList<RiskFinding> findings) =>
        findings
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(Impact)
            .ThenByDescending(f => f.Confidence.Value)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ThenBy(f => f.Id, StringComparer.Ordinal)
            .Take(TopRisksLimit)
            .ToList();
}
