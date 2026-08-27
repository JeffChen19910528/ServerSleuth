namespace ServerSleuth.Core.Models;

public sealed class OperatingSystem : DiscoveryEntity
{
    public string? Platform { get; init; } // e.g. "Windows Server 2022", "Ubuntu 22.04"
    public string? Kernel { get; init; }
    public string? Edition { get; init; }
}
