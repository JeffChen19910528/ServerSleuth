namespace ServerSleuth.Analysis.Correlation.Validation;

public sealed record OrphanFinding
{
    public required string EntityId { get; init; }
    public required string EntityType { get; init; }
    public required OrphanClassification Classification { get; init; }
    public required string Reason { get; init; }
}
