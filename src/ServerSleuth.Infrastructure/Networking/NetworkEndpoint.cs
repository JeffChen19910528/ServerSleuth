namespace ServerSleuth.Infrastructure.Networking;

public sealed record NetworkEndpoint
{
    public required string Protocol { get; init; } // "TCP", "UDP"
    public required string LocalAddress { get; init; }
    public required int LocalPort { get; init; }
    public int? ProcessId { get; init; }
    public string? ProcessName { get; init; }
    public required string State { get; init; } // "Listening", "Established", etc.
}
