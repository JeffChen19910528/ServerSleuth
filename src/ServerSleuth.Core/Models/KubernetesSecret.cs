namespace ServerSleuth.Core.Models;

/// <summary>Metadata about a Kubernetes Secret — see skill.md (Phase 6D) §15 (CRITICAL). Only
/// the Secret's name, type, and key names are ever captured. Values are NEVER retrieved,
/// decoded, or persisted anywhere — not in this entity, its metadata, evidence, diagnostics, or
/// logs.</summary>
public sealed class KubernetesSecret : DiscoveryEntity
{
    public string? Namespace { get; init; }
    public string? SecretType { get; init; }
    public IReadOnlyList<string> Keys { get; init; } = [];
}
