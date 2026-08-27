namespace ServerSleuth.Linux.Kubernetes;

public sealed record ServicePortRow
{
    public int? Port { get; init; }
    public string? TargetPort { get; init; }
    public int? NodePort { get; init; }
    public string? Protocol { get; init; }
}
