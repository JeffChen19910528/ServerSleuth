using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Infrastructure.Remote;

/// <summary>
/// <see cref="IProcessRunner"/> over one SSH "exec" channel per call — see skill.md (Phase
/// 10D-2) §9. The ONLY place a <see cref="ProcessRequest"/>'s discrete Executable/Arguments are
/// ever joined into one string is <see cref="SshCommandLineBuilder.Build"/>, called here and
/// nowhere else — no Linux provider/scanner builds a command string itself. Preserves
/// <see cref="IProcessRunner"/>'s existing contract exactly (same interface, same
/// <see cref="ProcessResult"/> shape scanners already consume) — this is what makes every
/// existing Linux provider that only depends on <see cref="IProcessRunner"/> automatically
/// remote-capable with zero changes to the provider itself (skill.md §21).
/// </summary>
public sealed class SshProcessRunner(ISshSession session) : IProcessRunner
{
    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;

        if (!session.IsConnected)
        {
            return Task.FromResult(new ProcessResult
            {
                Status = OperationStatus.TransportUnavailable,
                Duration = DateTimeOffset.UtcNow - startedAt
            });
        }

        var commandLine = SshCommandLineBuilder.Build(request.Executable, request.Arguments);
        var result = session.ExecuteCommand(commandLine, request.Timeout, cancellationToken);
        var duration = DateTimeOffset.UtcNow - startedAt;

        return Task.FromResult(result.Status switch
        {
            OperationStatus.Success => ProcessResult.Ok(result.ExitCode ?? 0, result.StandardOutput, result.StandardError, duration),
            OperationStatus.Timeout => ProcessResult.TimedOutResult(duration),
            OperationStatus.Cancelled => ProcessResult.CancelledResult(duration),
            _ => new ProcessResult { Status = result.Status, Duration = duration }
        });
    }
}
