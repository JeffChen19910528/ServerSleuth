namespace ServerSleuth.Core.Models;

/// <summary>A Kubernetes node — see skill.md (Phase 6D) §8. Facts only, as reported by the API;
/// never SSH'd into, never executed against.</summary>
public sealed class KubernetesNode : DiscoveryEntity
{
    public IReadOnlyList<string> Roles { get; init; } = [];
    public string? KubernetesVersion { get; init; }
    public string? OsImage { get; init; }
    public string? ContainerRuntimeVersion { get; init; }
    public string? KernelVersion { get; init; }
    public bool? Ready { get; init; }
}
