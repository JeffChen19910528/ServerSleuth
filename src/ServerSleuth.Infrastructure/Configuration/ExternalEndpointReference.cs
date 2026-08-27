namespace ServerSleuth.Infrastructure.Configuration;

public sealed record ExternalEndpointReference
{
    public required string Scheme { get; init; }
    public required string Host { get; init; }
    public int? Port { get; init; }
    public string? Path { get; init; }
}
