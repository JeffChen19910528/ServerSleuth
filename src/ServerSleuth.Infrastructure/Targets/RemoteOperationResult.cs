using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Infrastructure.Targets;

/// <summary>
/// The outcome of a single <see cref="RemoteOperation"/> — see skill.md (Phase 10D-1) §5.
/// Mirrors <see cref="Process.ProcessResult"/>'s shape (this codebase's existing convention for
/// "outcome of a structured operation, never an exception for an expected failure mode") rather
/// than introducing a differently-shaped result type, and reuses the shared
/// <see cref="OperationStatus"/> enum rather than a second, remote-only status enum.
///
/// Nothing in this codebase produces a real <see cref="RemoteOperationResult"/> yet — no
/// transport executes a <see cref="RemoteOperation"/> in this phase (skill.md §3, §26). This
/// type exists so a future SSH/WinRM transport has an already-defined, structured result shape
/// to return instead of inventing its own.
/// </summary>
public sealed record RemoteOperationResult
{
    public required OperationStatus Status { get; init; }
    public int? ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;

    /// <summary>Set for a non-<see cref="OperationStatus.Success"/> outcome — never a raw
    /// secret value (skill.md §8): a future transport must redact this the same way
    /// <see cref="RemoteOperation.DescribeForLogging"/> already redacts the operation itself.</summary>
    public string? ErrorMessage { get; init; }

    public TimeSpan Duration { get; init; }

    public bool Success => Status == OperationStatus.Success;

    public static RemoteOperationResult Ok(string standardOutput, string standardError, int exitCode, TimeSpan duration) => new()
    {
        Status = OperationStatus.Success,
        ExitCode = exitCode,
        StandardOutput = standardOutput,
        StandardError = standardError,
        Duration = duration
    };

    public static RemoteOperationResult Failure(OperationStatus status, string errorMessage, TimeSpan duration) => new()
    {
        Status = status,
        ErrorMessage = errorMessage,
        Duration = duration
    };

    public static RemoteOperationResult TransportUnavailableResult(string errorMessage, TimeSpan duration) =>
        Failure(OperationStatus.TransportUnavailable, errorMessage, duration);

    public static RemoteOperationResult ProtocolErrorResult(string errorMessage, TimeSpan duration) =>
        Failure(OperationStatus.ProtocolError, errorMessage, duration);

    public static RemoteOperationResult NotInstalledResult(TimeSpan duration) =>
        Failure(OperationStatus.NotInstalled, "The requested tool/service is not present on the target.", duration);

    public static RemoteOperationResult TimedOutResult(TimeSpan duration) =>
        new() { Status = OperationStatus.Timeout, Duration = duration };

    public static RemoteOperationResult CancelledResult(TimeSpan duration) =>
        new() { Status = OperationStatus.Cancelled, Duration = duration };
}
