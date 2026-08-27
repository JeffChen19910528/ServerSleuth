namespace ServerSleuth.Core.Models;

/// <summary>A Kubernetes namespace — see skill.md (Phase 6D) §7. Identity always includes the
/// cluster context, since namespace names are not globally unique across clusters.</summary>
public sealed class KubernetesNamespace : DiscoveryEntity
{
    public string? Phase { get; init; } // "Active", "Terminating"
    public string? Uid { get; init; }
}
