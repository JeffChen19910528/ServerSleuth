namespace ServerSleuth.Analysis.Correlation.Validation;

public sealed record GraphValidationResult
{
    public required IReadOnlyList<ValidationFinding> Findings { get; init; }
    public required IReadOnlyList<OrphanFinding> Orphans { get; init; }
    public required IReadOnlyList<CycleFinding> Cycles { get; init; }
    public required GraphValidationSummary Summary { get; init; }
}
