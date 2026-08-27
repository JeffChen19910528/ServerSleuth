namespace ServerSleuth.Linux.Kubernetes;

/// <summary>One container within a Pod — see skill.md (Phase 6D) §10. Never merged with a
/// host-level Docker/Podman container discovered by the Phase 6C scanner; that relationship, if
/// ever drawn, belongs to later Analysis.</summary>
public sealed record PodContainerRow
{
    public required string Name { get; init; }
    public string? Image { get; init; }
    public string? ImageId { get; init; }
    public string? State { get; init; } // "running", "waiting", "terminated"
    public bool? Ready { get; init; }
    public int? RestartCount { get; init; }
}
