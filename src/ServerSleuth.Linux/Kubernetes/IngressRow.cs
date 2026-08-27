namespace ServerSleuth.Linux.Kubernetes;

/// <summary>One discovered Ingress — see skill.md (Phase 6D) §13. Never contacted, never
/// DNS-resolved, never certificate-validated.</summary>
public sealed record IngressRow
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public string? Uid { get; init; }
    public string? IngressClassName { get; init; }
    public IReadOnlyList<string> Hosts { get; init; } = [];

    /// <summary>Already-formatted "{host}{path} -> {serviceName}:{servicePort}" strings.</summary>
    public IReadOnlyList<string> Paths { get; init; } = [];
    public IReadOnlyList<string> TlsSecretNames { get; init; } = [];
}
