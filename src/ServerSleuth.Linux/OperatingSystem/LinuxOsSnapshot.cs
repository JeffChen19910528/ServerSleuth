namespace ServerSleuth.Linux.OperatingSystem;

/// <summary>Raw facts captured from the machine, before mapping to domain entities — kept
/// separate so the mapping itself is pure/unit-testable without real files or process
/// execution.</summary>
public sealed record LinuxOsSnapshot
{
    public bool OsReleaseAvailable { get; init; }
    public IReadOnlyDictionary<string, string> OsRelease { get; init; } = new Dictionary<string, string>();

    public string? Hostname { get; init; }
    public string? KernelRelease { get; init; }
    public string? OsType { get; init; }
    public string? UnameMachine { get; init; }
}
