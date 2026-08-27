namespace ServerSleuth.Linux.Kubernetes;

/// <summary>One discovered Pod — see skill.md (Phase 6D) §9. PodIP/HostIP are recorded as facts
/// only; never probed, never connected to.</summary>
public sealed record PodRow
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public string? Uid { get; init; }
    public string? Phase { get; init; }
    public string? NodeName { get; init; }
    public string? PodIp { get; init; }
    public string? HostIp { get; init; }
    public DateTimeOffset? Created { get; init; }
    public IReadOnlyList<PodContainerRow> Containers { get; init; } = [];
}
