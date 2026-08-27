namespace ServerSleuth.Windows.IIS;

public sealed record IisProbeResult
{
    public required IisAvailability Status { get; init; }
    public IisSnapshot? Snapshot { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>Sites/pools that failed to read individually (partial failures) even though
    /// the overall probe succeeded — never silently dropped.</summary>
    public IReadOnlyList<string> PartialFailures { get; init; } = [];

    public static IisProbeResult NotInstalled() => new() { Status = IisAvailability.NotInstalled };

    public static IisProbeResult Available(IisSnapshot snapshot, IReadOnlyList<string>? partialFailures = null) => new()
    {
        Status = IisAvailability.Available,
        Snapshot = snapshot,
        PartialFailures = partialFailures ?? []
    };

    public static IisProbeResult Failure(IisAvailability status, string errorMessage) => new()
    {
        Status = status,
        ErrorMessage = errorMessage
    };
}
