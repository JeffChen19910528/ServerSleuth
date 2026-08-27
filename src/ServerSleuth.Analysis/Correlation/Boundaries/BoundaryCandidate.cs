using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Correlation.Boundaries;

/// <summary>
/// A workload boundary before/after the cross-workload merge step, but always considered
/// "confirmed" once built — skill.md §13's Candidate-vs-Confirmed distinction is realized here
/// as: every <see cref="WorkloadAnchor"/> becomes exactly one confirmed single-anchor
/// <see cref="BoundaryCandidate"/> immediately (its members come only from explicit skill.md §4
/// evidence sources, never a guess), and the only thing that remains genuinely provisional is
/// whether two such candidates get merged together — that decision, and every candidate merge
/// considered along the way (rejected or ambiguous), is recorded in
/// <see cref="Diagnostics.BoundaryDiagnostics"/>, never silently dropped.
/// </summary>
public sealed record BoundaryCandidate
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<string> AnchorEntityIds { get; init; }
    public WorkloadAnchorKind? SingleAnchorKind { get; init; }
    public IReadOnlyList<string> MemberEntityIds { get; init; } = [];
    public IReadOnlyList<EvidenceRecord> Evidence { get; init; } = [];
    public required Confidence Confidence { get; init; }
    public required string Reason { get; init; }
    public string? RootPath { get; init; }
}
