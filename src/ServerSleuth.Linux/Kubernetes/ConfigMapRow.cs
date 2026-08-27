namespace ServerSleuth.Linux.Kubernetes;

/// <summary>One discovered ConfigMap — see skill.md (Phase 6D) §14. `RawTextData` values are
/// still raw here; redaction happens once, in the mapping to a domain entity, never persisted
/// raw beyond that point. `BinaryDataKeys` never carries values at all.</summary>
public sealed record ConfigMapRow
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public IReadOnlyDictionary<string, string> RawTextData { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> BinaryDataKeys { get; init; } = [];
}
