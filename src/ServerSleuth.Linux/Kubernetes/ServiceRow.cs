namespace ServerSleuth.Linux.Kubernetes;

/// <summary>One discovered Kubernetes Service — see skill.md (Phase 6D) §12. No network probe
/// is ever performed and no listening-process is inferred from this data.</summary>
public sealed record ServiceRow
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public string? Uid { get; init; }
    public string? ServiceType { get; init; }
    public string? ClusterIp { get; init; }
    public IReadOnlyList<string> ExternalIps { get; init; } = [];
    public IReadOnlyList<ServicePortRow> Ports { get; init; } = [];
    public IReadOnlyDictionary<string, string> SelectorLabels { get; init; } = new Dictionary<string, string>();
}
