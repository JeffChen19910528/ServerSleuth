namespace ServerSleuth.Core.Models;

/// <summary>.NET/Java/Python/Node/etc. runtime — see skill.md §11.</summary>
public sealed class Runtime : DiscoveryEntity
{
    public string? DetectionCommand { get; init; }
}
