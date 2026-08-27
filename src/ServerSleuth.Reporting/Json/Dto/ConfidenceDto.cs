namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>Mirrors <see cref="ServerSleuth.Core.Evidence.Confidence"/> — the raw 0.00-1.00 value
/// plus its fixed band, never recomputed.</summary>
public sealed record ConfidenceDto
{
    public required double Value { get; init; }
    public required string Band { get; init; }
}
