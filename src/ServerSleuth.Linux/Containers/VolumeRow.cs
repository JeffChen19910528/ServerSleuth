namespace ServerSleuth.Linux.Containers;

/// <summary>One discovered named volume — see skill.md (Phase 6C) §10. Never mounted, never
/// read.</summary>
public sealed record VolumeRow
{
    public required string Name { get; init; }
    public string? Driver { get; init; }
    public string? Mountpoint { get; init; }
    public IReadOnlyDictionary<string, string> RawLabels { get; init; } = new Dictionary<string, string>();
}
