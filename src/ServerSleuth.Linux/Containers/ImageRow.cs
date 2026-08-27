namespace ServerSleuth.Linux.Containers;

/// <summary>One discovered image — see skill.md (Phase 6C) §7. Never pulled, never inspected
/// against a remote registry; sourced entirely from local runtime metadata.</summary>
public sealed record ImageRow
{
    public required string ImageId { get; init; }
    public string? Repository { get; init; }
    public string? Tag { get; init; }
    public DateTimeOffset? Created { get; init; }

    /// <summary>The runtime's own human-formatted size string (e.g. "142MB") — the CLI's list
    /// output does not expose a raw byte count, and converting a formatted string back to an
    /// exact byte count is not attempted (would not be a fact read from the system, but a
    /// re-derived approximation).</summary>
    public string? SizeDisplay { get; init; }
}
