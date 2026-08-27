namespace ServerSleuth.Linux.Kubernetes;

/// <summary>One discovered node — see skill.md (Phase 6D) §8. Never SSH'd into, never executed
/// against.</summary>
public sealed record NodeRow
{
    public required string Name { get; init; }
    public string? Uid { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public string? KubeletVersion { get; init; }
    public string? OsImage { get; init; }
    public string? ContainerRuntimeVersion { get; init; }
    public string? KernelVersion { get; init; }
    public bool? Ready { get; init; }
}
