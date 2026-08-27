namespace ServerSleuth.Core.Models;

/// <summary>IIS site — see skill.md §8.</summary>
public sealed class WebSite : DiscoveryEntity
{
    public string? PhysicalPath { get; init; }
    public IReadOnlyList<string> Bindings { get; init; } = [];
    public string? Protocol { get; init; }
    public string? HostName { get; init; }
    public int? Port { get; init; }
    public string? CertificateThumbprint { get; init; }
    public string? Authentication { get; init; }
}
