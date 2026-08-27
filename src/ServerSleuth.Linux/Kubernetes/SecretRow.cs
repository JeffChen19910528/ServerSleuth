namespace ServerSleuth.Linux.Kubernetes;

/// <summary>Metadata about one discovered Secret — see skill.md (Phase 6D) §15 (CRITICAL). Only
/// key NAMES ever reach this row; the provider never even deserializes the `data`/`stringData`
/// values into a usable form, let alone stores them.</summary>
public sealed record SecretRow
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public string? SecretType { get; init; }
    public IReadOnlyList<string> Keys { get; init; } = [];
}
