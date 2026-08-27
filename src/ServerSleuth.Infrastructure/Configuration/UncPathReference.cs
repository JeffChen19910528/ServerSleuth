namespace ServerSleuth.Infrastructure.Configuration;

public sealed record UncPathReference
{
    public required string Server { get; init; }
    public required string Share { get; init; }
    public string? Path { get; init; }
}
