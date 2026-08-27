using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Core.Results;

/// <summary>
/// The outcome of a single scanner's run: the entities it found (possibly none), any errors
/// it hit along the way, and an overall status. A scanner can be PartiallySupported and still
/// return entities alongside errors — the two are not mutually exclusive. See skill.md §25-26.
/// </summary>
public sealed record DiscoveryResult
{
    public required string ScannerId { get; init; }
    public required ScannerStatus Status { get; init; }
    public IReadOnlyList<DiscoveryEntity> Entities { get; init; } = [];
    public IReadOnlyList<DiscoveryError> Errors { get; init; } = [];

    public static DiscoveryResult Success(string scannerId, IReadOnlyList<DiscoveryEntity> entities) =>
        new() { ScannerId = scannerId, Status = ScannerStatus.Supported, Entities = entities };

    public static DiscoveryResult Failure(string scannerId, DiscoveryError error) =>
        new() { ScannerId = scannerId, Status = ScannerStatus.Failed, Errors = [error] };
}
