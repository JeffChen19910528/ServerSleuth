using ServerSleuth.Core.Enums;

namespace ServerSleuth.Core.Evidence;

/// <summary>
/// A single piece of proof that caused an entity or dependency to be identified.
/// Every non-Unknown-status entity must carry at least one of these — see skill.md §6.
/// </summary>
public sealed record EvidenceRecord
{
    public required EvidenceType Type { get; init; }

    /// <summary>Where the evidence was found, e.g. a registry key path, a file path, a process name.</summary>
    public required string Location { get; init; }

    /// <summary>Free-form supporting detail. Must never contain secret values — callers are
    /// responsible for redacting before constructing this record (see SecretRedactor, Phase 2).</summary>
    public string? Detail { get; init; }

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}
