using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Core.Orchestration;

/// <summary>
/// The deterministic result of running every registered scanner once — see skill.md
/// (Phase 6G) §7. Never silently discards a scanner failure: every scanner's own
/// <see cref="DiscoveryResult"/> (entities, errors, and status) is preserved in
/// <see cref="ScannerResults"/> exactly as that scanner produced it, in registry order — a
/// scanner that came back <see cref="ScannerStatus.AccessDenied"/> remains fully visible here,
/// never dropped because it found nothing.
/// </summary>
public sealed record AggregateDiscoveryResult
{
    /// <summary>Every entity from every scanner, concatenated in scanner-registry order — never
    /// re-sorted, never deduplicated here. Phase 5 correlation (Analysis), not this engine,
    /// decides whether two observations describe the same logical entity.</summary>
    public required IReadOnlyList<DiscoveryEntity> Entities { get; init; }

    /// <summary>Every error from every scanner, in scanner-registry order.</summary>
    public required IReadOnlyList<DiscoveryError> Errors { get; init; }

    /// <summary>Each scanner's own full result, one per registered scanner, in registry order —
    /// the authoritative per-scanner record; <see cref="Entities"/>/<see cref="Errors"/> are
    /// simply this list flattened for convenience.</summary>
    public required IReadOnlyList<DiscoveryResult> ScannerResults { get; init; }

    /// <summary>Scanner ID → that scanner's own <see cref="ScannerStatus"/>, for quick lookup
    /// without re-scanning <see cref="ScannerResults"/>.</summary>
    public required IReadOnlyDictionary<string, ScannerStatus> ScannerStatuses { get; init; }

    /// <summary>Engine-level notes not attributable to any single scanner's own error list —
    /// e.g. "scanner X threw an unhandled exception" (the engine catches it and degrades that
    /// scanner to Failed rather than aborting the whole run, but still records what happened).</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}
