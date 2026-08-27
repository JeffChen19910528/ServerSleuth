namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>Mirrors <see cref="ServerSleuth.Analysis.Correlation.Validation.ValidationFinding"/> —
/// passed through unmodified from Phase 5D/8C, never re-evaluated here.</summary>
public sealed record GraphValidationFindingDto
{
    public required string Category { get; init; }
    public required string Code { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public required IReadOnlyList<string> EntityIds { get; init; }
}
