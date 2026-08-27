namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>Mirrors <see cref="ServerSleuth.Analysis.Migration.Consolidation.CoverageWarning"/> —
/// <c>Evidence</c> here is the scanner's own recorded <c>DiscoveryError.Message</c> strings, never
/// raw scanner output/configuration content (Phase 8C already restricts it to that).</summary>
public sealed record CoverageWarningDto
{
    public required string ScannerId { get; init; }
    public required string ScannerStatus { get; init; }
    public required string Reason { get; init; }
    public required string AffectedPlatform { get; init; }
    public required IReadOnlyList<string> Evidence { get; init; }
}
