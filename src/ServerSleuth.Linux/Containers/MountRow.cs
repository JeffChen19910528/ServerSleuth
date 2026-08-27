namespace ServerSleuth.Linux.Containers;

/// <summary>One container mount (bind/volume/tmpfs) — see skill.md (Phase 6C) §9. The mounted
/// filesystem/volume content is never accessed; only the inspect-reported facts are recorded.</summary>
public sealed record MountRow
{
    public required string Type { get; init; }
    public string? Source { get; init; }
    public required string Destination { get; init; }
    public bool ReadOnly { get; init; }
    public string? Propagation { get; init; }
}
