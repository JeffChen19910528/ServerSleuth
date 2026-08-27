namespace ServerSleuth.Core.Models;

/// <summary>A Kubernetes workload controller — Deployment, StatefulSet, or DaemonSet — see
/// skill.md (Phase 6D) §11. Never executed, never scaled; <see cref="Kind"/> distinguishes the
/// three, since they are genuinely different resource types sharing one portable shape.</summary>
public sealed class KubernetesWorkload : DiscoveryEntity
{
    public required string Kind { get; init; } // "Deployment", "StatefulSet", "DaemonSet"
    public string? Namespace { get; init; }
    public string? Uid { get; init; }
    public int? DesiredReplicas { get; init; }
    public int? ReadyReplicas { get; init; }
    public IReadOnlyList<string> SelectorLabels { get; init; } = [];
    public IReadOnlyList<string> TemplateContainerImages { get; init; } = [];
}
