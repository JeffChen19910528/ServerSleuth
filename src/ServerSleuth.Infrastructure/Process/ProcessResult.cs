using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Infrastructure.Process;

public sealed record ProcessResult
{
    public required OperationStatus Status { get; init; }
    public int? ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public bool TimedOut { get; init; }
    public bool Cancelled { get; init; }
    public TimeSpan Duration { get; init; }

    public bool Success => Status == OperationStatus.Success && ExitCode == 0;

    public static ProcessResult Ok(int exitCode, string stdOut, string stdErr, TimeSpan duration) => new()
    {
        Status = OperationStatus.Success,
        ExitCode = exitCode,
        StandardOutput = stdOut,
        StandardError = stdErr,
        Duration = duration
    };

    public static ProcessResult TimedOutResult(TimeSpan duration) => new()
    {
        Status = OperationStatus.Timeout,
        TimedOut = true,
        Duration = duration
    };

    public static ProcessResult CancelledResult(TimeSpan duration) => new()
    {
        Status = OperationStatus.Cancelled,
        Cancelled = true,
        Duration = duration
    };

    public static ProcessResult StartFailedResult(TimeSpan duration) => new()
    {
        Status = OperationStatus.StartFailed,
        Duration = duration
    };
}
