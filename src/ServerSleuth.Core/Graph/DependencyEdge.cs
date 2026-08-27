using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Core.Graph;

/// <summary>
/// A directed relationship between two entities. Every edge must carry Evidence and a
/// Confidence — an edge without either is a correlation bug, not an acceptable placeholder.
/// See skill.md §21, §40.
/// </summary>
public sealed record DependencyEdge
{
    public required string SourceEntityId { get; init; }
    public required string TargetEntityId { get; init; }
    public required DependencyEdgeType Type { get; init; }
    public required Confidence Confidence { get; init; }
    public IReadOnlyList<EvidenceRecord> Evidence { get; init; } = [];
}
