namespace ServerSleuth.Windows.Registry;

/// <summary>
/// The payload of a successful <see cref="WindowsRegistryQuery"/> — populated according to
/// which of <see cref="WindowsRegistryQuery.IncludeSubKeys"/>/<see cref="WindowsRegistryQuery.IncludeValues"/>
/// were requested (an unrequested field is simply an empty collection, never used to signal
/// failure — failure is <see cref="Remote.WindowsRemoteOperationResult{T}.Status"/>, exactly
/// like every other result type in this codebase).
/// </summary>
public sealed record WindowsRegistryQueryResult
{
    public IReadOnlyList<string> SubKeyNames { get; init; } = [];

    /// <summary>Missing values are simply absent from the map, never inferred — the same
    /// convention <see cref="IWindowsRegistryReader.GetValues"/> already documents.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; init; } = new Dictionary<string, object?>();
}
