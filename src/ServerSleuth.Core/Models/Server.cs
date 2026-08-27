namespace ServerSleuth.Core.Models;

public sealed class Server : DiscoveryEntity
{
    public string? Hostname { get; init; }
    public string? Domain { get; init; }
    public IReadOnlyList<string> IpAddresses { get; init; } = [];
}
