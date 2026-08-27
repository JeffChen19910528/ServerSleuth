namespace ServerSleuth.Core.Models;

/// <summary>A Kubernetes Ingress — see skill.md (Phase 6D) §13. Never contacted, never
/// DNS-resolved, never certificate-validated; only the resource's own declared references are
/// recorded.</summary>
public sealed class KubernetesIngress : DiscoveryEntity
{
    public string? Namespace { get; init; }
    public string? Uid { get; init; }
    public string? IngressClassName { get; init; }
    public IReadOnlyList<string> Hosts { get; init; } = [];

    /// <summary>Each entry formatted as "{host}{path} -> {serviceName}:{servicePort}".</summary>
    public IReadOnlyList<string> Paths { get; init; } = [];
    public IReadOnlyList<string> TlsSecretNames { get; init; } = [];
}
