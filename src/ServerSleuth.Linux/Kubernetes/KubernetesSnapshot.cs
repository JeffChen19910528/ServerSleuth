namespace ServerSleuth.Linux.Kubernetes;

public sealed record KubernetesSnapshot
{
    public required KubernetesAvailability Status { get; init; }
    public string? ErrorMessage { get; init; }
    public ClusterRow? Cluster { get; init; }
    public IReadOnlyList<NamespaceRow> Namespaces { get; init; } = [];
    public IReadOnlyList<NodeRow> Nodes { get; init; } = [];
    public IReadOnlyList<PodRow> Pods { get; init; } = [];
    public IReadOnlyList<WorkloadRow> Workloads { get; init; } = [];
    public IReadOnlyList<ServiceRow> Services { get; init; } = [];
    public IReadOnlyList<IngressRow> Ingresses { get; init; } = [];
    public IReadOnlyList<ConfigMapRow> ConfigMaps { get; init; } = [];
    public IReadOnlyList<SecretRow> Secrets { get; init; } = [];
    public IReadOnlyList<PvcRow> Pvcs { get; init; } = [];
    public IReadOnlyList<PvRow> Pvs { get; init; } = [];
    public IReadOnlyList<string> PartialFailures { get; init; } = [];
}
