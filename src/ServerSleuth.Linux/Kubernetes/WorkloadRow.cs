namespace ServerSleuth.Linux.Kubernetes;

/// <summary>One Deployment/StatefulSet/DaemonSet — see skill.md (Phase 6D) §11. Never executed,
/// never scaled.</summary>
public sealed record WorkloadRow
{
    public required string Kind { get; init; } // "Deployment", "StatefulSet", "DaemonSet"
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public string? Uid { get; init; }
    public int? DesiredReplicas { get; init; }
    public int? ReadyReplicas { get; init; }
    public IReadOnlyDictionary<string, string> SelectorLabels { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> TemplateContainerImages { get; init; } = [];
}
