using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Risk.Models;

/// <summary>
/// Common fields shared by every aggregate summary (<see cref="ApplicationRiskSummary"/>,
/// <see cref="ServerRiskSummary"/>) — see skill.md (Phase 7B) §3. Never a 0-100 score: only a
/// severity classification plus explicit counts/metrics, all derivable from the underlying
/// <c>Findings</c> list, which is never a deep copy — it stores the exact same RiskFinding
/// instances Phase 7A produced, so provenance is never duplicated or diverges from its source.
/// </summary>
public abstract record RiskSummaryBase
{
    /// <summary>See skill.md (Phase 7B) §2 for the exact escalation policy: the highest severity
    /// present among <c>Findings</c>, or <see cref="AggregateSeverity.None"/> if empty. A single
    /// Critical finding always makes this Critical — an aggregate can never "average away" or
    /// otherwise hide it.</summary>
    public required AggregateSeverity OverallSeverity { get; init; }

    public required int CriticalCount { get; init; }
    public required int HighCount { get; init; }
    public required int MediumCount { get; init; }
    public required int LowCount { get; init; }
    public required int InfoCount { get; init; }
    public required int TotalFindingCount { get; init; }

    /// <summary>Count of distinct entity Ids referenced by <c>Findings</c> — each finding's
    /// <c>SourceEntityId</c> plus every <c>RelatedEntityIds</c> entry, deduplicated. Never
    /// inferred from naming similarity; only from the explicit Ids the findings already carry.</summary>
    public required int AffectedEntityCount { get; init; }

    /// <summary>Count of distinct <c>ApplicationBoundaryId</c> values touched by <c>Findings</c>
    /// — see skill.md (Phase 7B) §4, §8. For an <see cref="ApplicationRiskSummary"/> this is
    /// always 0 or 1 (its own boundary); it is meaningful at the <see cref="ServerRiskSummary"/>
    /// level, where it reports how many distinct applications are affected.</summary>
    public required int AffectedBoundaryCount { get; init; }

    /// <summary>The exact same RiskFinding instances Phase 7A produced for this summary's
    /// scope — never re-copied or re-identified.</summary>
    public required IReadOnlyList<Risk.Models.RiskFinding> Findings { get; init; }

    /// <summary>Deterministically ordered subset of <c>Findings</c> — see skill.md (Phase 7B)
    /// §11 for the exact ordering rule (Severity desc, Impact desc, Confidence desc, RuleId
    /// ordinal, FindingId ordinal). Never dictionary/registration order.</summary>
    public required IReadOnlyList<Risk.Models.RiskFinding> TopRisks { get; init; }

    /// <summary>Finding count per <see cref="RiskCategory"/> — see skill.md (Phase 7B) §12.
    /// Categories with zero findings in this scope are omitted rather than listed as 0.</summary>
    public required IReadOnlyDictionary<RiskCategory, int> CategoryCounts { get; init; }

    /// <summary>Count of findings whose <c>Category</c> is <see cref="RiskCategory.SharedInfrastructure"/>
    /// — see skill.md (Phase 7B) §9. Deliberately does not raise <c>OverallSeverity</c> merely
    /// because a dependency is shared: impact and severity are kept as separate concepts.</summary>
    public required int SharedDependencyCount { get; init; }

    /// <summary>Conservative aggregate confidence — see skill.md (Phase 7B) §10. Equal to the
    /// highest single <c>Confidence.Value</c> among <c>Findings</c> in this scope (i.e. "the
    /// confidence of the single most-confident contributing finding"), never a sum/average/
    /// Bayesian combination that could push the aggregate above what any one piece of evidence
    /// individually supports. <c>Confidence(0.0)</c> when <c>Findings</c> is empty.</summary>
    public required Confidence AggregateConfidence { get; init; }
}
