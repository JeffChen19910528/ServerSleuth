namespace ServerSleuth.Linux.Containers;

/// <summary>One container's already-gathered raw facts from `&lt;runtime&gt; inspect` — see
/// skill.md (Phase 6C) §5-6. Environment/Entrypoint/Command/Labels are still raw here; redaction
/// happens once, in the mapping to a domain entity, never persisted raw beyond that point.</summary>
public sealed record ContainerRow
{
    public required string ContainerId { get; init; }
    public string? Name { get; init; }
    public string? Image { get; init; }
    public string? ImageId { get; init; }
    public DateTimeOffset? Created { get; init; }
    public string? State { get; init; }
    public string? Status { get; init; }
    public string? RestartPolicy { get; init; }
    public string? Entrypoint { get; init; }
    public string? Command { get; init; }
    public int? Pid { get; init; }
    public IReadOnlyList<string> RawEnvironmentVariables { get; init; } = [];
    public IReadOnlyDictionary<string, string> RawLabels { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<PortMappingRow> Ports { get; init; } = [];
    public IReadOnlyList<MountRow> Mounts { get; init; } = [];
    public IReadOnlyList<string> NetworkNames { get; init; } = [];
}
