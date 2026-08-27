namespace ServerSleuth.Linux.Containers;

/// <summary>One discovered container network — see skill.md (Phase 6C) §11. Never connected
/// to, never probed.</summary>
public sealed record NetworkRow
{
    public required string NetworkId { get; init; }
    public required string Name { get; init; }
    public string? Driver { get; init; }
    public string? Subnet { get; init; }
    public string? Gateway { get; init; }
    public IReadOnlyList<string> AttachedContainerNames { get; init; } = [];
}
