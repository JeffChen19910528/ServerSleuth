namespace ServerSleuth.Core.Results;

/// <summary>
/// A recorded scanner failure. Producing one of these — never an unhandled exception —
/// is how a scanner reports trouble without aborting the overall scan. See skill.md §26.
/// </summary>
public sealed record DiscoveryError
{
    public required string ScannerId { get; init; }
    public required string Message { get; init; }
    public bool IsPermissionFailure { get; init; }
    public string? Exception { get; init; }
}
