using ServerSleuth.Core.Enums;

namespace ServerSleuth.Infrastructure.Runtimes;

/// <summary>
/// One detector's outcome. Status reuses Core's ScannerStatus vocabulary at detector
/// granularity: NotInstalled means "this runtime family was not detected" (skill.md's
/// "NotDetected" language), never Failed — a missing runtime is a normal, expected outcome.
/// </summary>
public sealed record RuntimeDetectionResult
{
    public required ScannerStatus Status { get; init; }
    public IReadOnlyList<RuntimeDetectionRow> Rows { get; init; } = [];
    public string? ErrorMessage { get; init; }

    public static RuntimeDetectionResult NotDetected() => new() { Status = ScannerStatus.NotInstalled };
    public static RuntimeDetectionResult Detected(IReadOnlyList<RuntimeDetectionRow> rows) => new() { Status = ScannerStatus.Supported, Rows = rows };
    public static RuntimeDetectionResult Partial(IReadOnlyList<RuntimeDetectionRow> rows, string errorMessage) =>
        new() { Status = ScannerStatus.PartiallySupported, Rows = rows, ErrorMessage = errorMessage };
    public static RuntimeDetectionResult Failure(string errorMessage) => new() { Status = ScannerStatus.Failed, ErrorMessage = errorMessage };
}
