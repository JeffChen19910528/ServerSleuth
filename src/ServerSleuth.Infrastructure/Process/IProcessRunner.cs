namespace ServerSleuth.Infrastructure.Process;

/// <summary>
/// Runs an external command without going through a shell, so scanners never build a
/// shell-command string from discovered/user-influenced data. See skill.md §35.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}
