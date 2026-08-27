namespace ServerSleuth.Linux.Kubernetes;

public sealed record NamespaceRow
{
    public required string Name { get; init; }
    public string? Uid { get; init; }
    public string? Phase { get; init; }
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();
}
