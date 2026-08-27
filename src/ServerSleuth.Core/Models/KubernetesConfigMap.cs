namespace ServerSleuth.Core.Models;

/// <summary>A Kubernetes ConfigMap — see skill.md (Phase 6D) §14. Key names are always captured;
/// text (`data`) values pass through <c>ISecretRedactor</c> before ever being stored, and binary
/// (`binaryData`) values are never captured at all, only their key names.</summary>
public sealed class KubernetesConfigMap : DiscoveryEntity
{
    public string? Namespace { get; init; }
    public IReadOnlyList<string> Keys { get; init; } = [];
}
