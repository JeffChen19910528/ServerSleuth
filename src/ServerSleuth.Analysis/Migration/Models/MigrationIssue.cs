using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Migration.Models;

/// <summary>
/// One RiskFinding, reinterpreted as a migration consequence by <c>MigrationPolicy</c> — see
/// skill.md (Phase 8A) §4. Always traceable back to exactly the RiskFinding that produced it
/// (<see cref="SourceRiskFindingId"/>/<see cref="RuleId"/>) and carries that finding's own
/// evidence unchanged — never fabricated, never amplified. One MigrationIssue per contributing
/// RiskFinding; Phase 7A's own deduplication (the `MissingBinaryEntityId` merge anchor) already
/// collapsed logically-identical findings before Migration Assessment ever sees them, so no
/// further issue-level merging happens here.
/// </summary>
public sealed record MigrationIssue
{
    /// <summary>Deterministic: <c>migration:{SourceRiskFindingId}</c> — never a random GUID.</summary>
    public required string IssueId { get; init; }

    public required string Title { get; init; }
    public required string Description { get; init; }

    /// <summary>The originating RiskFinding's own severity — carried through unchanged, never
    /// recomputed. Migration Status is a SEPARATE classification (see <see cref="MigrationStatusImpact"/>);
    /// this field exists purely for traceability/explanation, not as a second scoring input.</summary>
    public required RiskSeverity Severity { get; init; }

    public required MigrationStatusImpact MigrationStatusImpact { get; init; }

    public required string RuleId { get; init; }
    public required string SourceRiskFindingId { get; init; }

    /// <summary>Every ApplicationBoundary this issue affects — ordinal-sorted. For a shared
    /// dependency this legitimately lists more than one boundary (skill.md §9); the same
    /// logical issue is never duplicated per boundary, only its attribution is.</summary>
    public required IReadOnlyList<string> AffectedBoundaryIds { get; init; }

    /// <summary>The source RiskFinding's SourceEntityId plus every RelatedEntityIds entry,
    /// ordinal-sorted and deduplicated.</summary>
    public required IReadOnlyList<string> AffectedEntityIds { get; init; }

    /// <summary>The originating RiskFinding's own evidence, unchanged — never fabricated.</summary>
    public required IReadOnlyList<EvidenceRecord> Evidence { get; init; }

    /// <summary>Never exceeds the originating RiskFinding's own confidence — see skill.md §14.</summary>
    public required Confidence Confidence { get; init; }

    public required string RequiredAction { get; init; }

    /// <summary>Machine-readable policy decision trace — see skill.md §16. Never rely on
    /// <see cref="RequiredAction"/>'s free text alone for programmatic decisions.</summary>
    public required string PolicyDecisionReason { get; init; }

    public static string ComputeId(string sourceRiskFindingId) => $"migration:{sourceRiskFindingId}";
}
