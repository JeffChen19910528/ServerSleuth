namespace ServerSleuth.Core.Models;

/// <summary>See skill.md §15. Never carries the private key.</summary>
public sealed class Certificate : DiscoveryEntity
{
    public string? Subject { get; init; }
    public string? Issuer { get; init; }
    public string? Thumbprint { get; init; }
    public DateTimeOffset? ValidFrom { get; init; }
    public DateTimeOffset? ValidTo { get; init; }
    public IReadOnlyList<string> SubjectAlternativeNames { get; init; } = [];
    public IReadOnlyList<string> UsedByEntityIds { get; init; } = [];
}
