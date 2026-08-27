using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Infrastructure.Remote;

/// <summary>Outcome of one <see cref="ISshSession.ExecuteCommand"/> call — the seam
/// <see cref="SshProcessRunner"/> translates into a <see cref="Process.ProcessResult"/>.</summary>
public sealed record SshCommandExecutionResult
{
    public required OperationStatus Status { get; init; }
    public int? ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;

    public static SshCommandExecutionResult Ok(int exitCode, string standardOutput, string standardError) => new()
    {
        Status = OperationStatus.Success,
        ExitCode = exitCode,
        StandardOutput = standardOutput,
        StandardError = standardError
    };

    public static SshCommandExecutionResult TimedOut() => new() { Status = OperationStatus.Timeout };

    public static SshCommandExecutionResult Cancelled() => new() { Status = OperationStatus.Cancelled };

    public static SshCommandExecutionResult TransportUnavailable() => new() { Status = OperationStatus.TransportUnavailable };
}
