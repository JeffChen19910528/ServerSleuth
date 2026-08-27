using ServerSleuth.Core.Enums;

namespace ServerSleuth.Analysis.Correlation.Validation;

public sealed record CycleFinding
{
    public required string CycleId { get; init; }
    public required IReadOnlyList<string> NodeIds { get; init; }
    public required IReadOnlyList<string> EdgeDescriptions { get; init; }
    public required IReadOnlyList<DependencyEdgeType> RelationshipTypes { get; init; }
    public required CycleClassification Classification { get; init; }
}
