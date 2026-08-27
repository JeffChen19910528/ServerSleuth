using ServerSleuth.Core.Enums;

namespace ServerSleuth.Analysis.Migration.Consolidation;

/// <summary>
/// One scanner whose own outcome means part of the discovery evidence this assessment is built
/// on is incomplete — see skill.md (Phase 8C) §13. Built strictly from an already-produced
/// <see cref="ServerSleuth.Core.Results.DiscoveryResult"/>: never fabricated, never inferred from
/// absence of data alone.
/// </summary>
public sealed record CoverageWarning
{
    public required string ScannerId { get; init; }
    public required ScannerStatus ScannerStatus { get; init; }

    /// <summary>The scanner's own recorded errors, joined — or a generic status note when the
    /// scanner reported no specific error (e.g. a bare PartiallySupported). Never invented text
    /// attributed to the scanner that it didn't actually report.</summary>
    public required string Reason { get; init; }

    /// <summary>"Windows"/"Linux"/"Unknown" — derived from the scanner Id's own established
    /// `windows-*`/`linux-*` naming convention (every scanner in this codebase already follows
    /// it), never a separately-tracked platform field that could drift out of sync.</summary>
    public required string AffectedPlatform { get; init; }

    /// <summary>The scanner's own <c>DiscoveryError.Message</c> values — the only evidence a
    /// scanner failure actually carries. Empty when the scanner reported no specific errors.</summary>
    public required IReadOnlyList<string> Evidence { get; init; }
}
