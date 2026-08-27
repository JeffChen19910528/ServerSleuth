namespace ServerSleuth.Windows.Services;

/// <summary>What the Service Control Manager (via ServiceController) reliably exposes.</summary>
public sealed record ServiceSnapshot
{
    public required string ServiceName { get; init; }
    public required string DisplayName { get; init; }
    public required string Status { get; init; }
    public required string ServiceType { get; init; }
}
