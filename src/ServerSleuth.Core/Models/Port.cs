namespace ServerSleuth.Core.Models;

public sealed class Port : DiscoveryEntity
{
    public required string Protocol { get; init; } // TCP / UDP
    public required string LocalAddress { get; init; }
    public required int Number { get; init; }
    public int? OwningPid { get; init; }
}
