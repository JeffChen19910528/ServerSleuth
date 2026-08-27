namespace ServerSleuth.Core.Models;

/// <summary>A Kubernetes Service — see skill.md (Phase 6D) §12. No network probe is ever
/// performed and no listening-process inference is made from this data.</summary>
public sealed class KubernetesService : DiscoveryEntity
{
    public string? Namespace { get; init; }
    public string? Uid { get; init; }
    public string? ServiceType { get; init; } // "ClusterIP", "NodePort", "LoadBalancer", "ExternalName"
    public string? ClusterIp { get; init; }
    public IReadOnlyList<string> ExternalIps { get; init; } = [];

    /// <summary>Each entry formatted as "{port}:{targetPort}/{protocol}" (plus "@{nodePort}"
    /// when present) — a display-string list, mirroring Container.Ports from Phase 6C.</summary>
    public IReadOnlyList<string> Ports { get; init; } = [];
    public IReadOnlyList<string> SelectorLabels { get; init; } = [];
}
