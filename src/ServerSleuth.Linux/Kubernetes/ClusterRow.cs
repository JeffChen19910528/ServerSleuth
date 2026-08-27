namespace ServerSleuth.Linux.Kubernetes;

/// <summary>The current kubectl context's cluster facts — see skill.md (Phase 6D) §6. Never
/// carries kubeconfig credentials.</summary>
public sealed record ClusterRow
{
    public string? ServerVersion { get; init; }
    public string? ContextName { get; init; }
    public bool? IsCurrentContext { get; init; }
    public string? ClusterIdentifier { get; init; }
}
