namespace ServerSleuth.Core.Models;

/// <summary>A Kubernetes cluster, as observed through the currently configured kubectl context —
/// see skill.md (Phase 6D) §6. Never carries kubeconfig credentials (client certs/keys/tokens);
/// only the context name and server version are recorded.</summary>
public sealed class Cluster : DiscoveryEntity
{
    public string? ServerVersion { get; init; }
    public string? ContextName { get; init; }
    public bool? IsCurrentContext { get; init; }

    /// <summary>Only set when a stable identifier is directly observable from the API (e.g. the
    /// kube-system namespace's UID) — never a randomly generated or guessed value.</summary>
    public string? ClusterIdentifier { get; init; }
}
