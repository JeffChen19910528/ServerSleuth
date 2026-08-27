using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Risk.Models;

/// <summary>
/// One evidence-backed, auditable migration risk — see skill.md (Phase 7A) §3. Never carries a
/// 0-100 score (that is Phase 7B's job); this phase only ever produces Category/Severity/
/// Confidence plus the concrete evidence and provenance that justify the finding.
/// </summary>
public sealed record RiskFinding
{
    /// <summary>Deterministic — see <see cref="ComputeId"/>. Never a random GUID: the same
    /// input must always produce the same Id, so re-running Risk Analysis against unchanged
    /// discovery data never appears as "new" findings in a diff.</summary>
    public required string Id { get; init; }

    public required string RuleId { get; init; }
    public required RiskCategory Category { get; init; }
    public required RiskSeverity Severity { get; init; }

    /// <summary>Must never exceed the confidence of the evidence/entity/edge that justified
    /// this finding — each rule is responsible for this invariant; see skill.md (Phase 7A) §6.</summary>
    public required Confidence Confidence { get; init; }

    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string SourceEntityId { get; init; }
    public IReadOnlyList<string> RelatedEntityIds { get; init; } = [];

    /// <summary>Every non-Info finding must have at least one entry — enforced by
    /// <see cref="Engine.RiskRuleEngine"/>, not merely documented. See skill.md §7.</summary>
    public IReadOnlyList<EvidenceRecord> Evidence { get; init; } = [];

    public required string Recommendation { get; init; }

    public string? ApplicationBoundaryId { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Identity rule: <c>risk:{RuleId}:{SourceEntityId}:{normalized-related-ids}</c>, where
    /// normalized-related-ids is the related entity IDs sorted ordinally and joined with `,` —
    /// so the same logical finding always produces the same Id regardless of the order a rule
    /// happened to discover its related entities in. See skill.md (Phase 7A) §3.
    /// </summary>
    public static string ComputeId(string ruleId, string sourceEntityId, IEnumerable<string>? relatedEntityIds = null)
    {
        var normalized = relatedEntityIds is null
            ? string.Empty
            : string.Join(",", relatedEntityIds.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal));

        return $"risk:{ruleId}:{sourceEntityId}:{normalized}";
    }
}
