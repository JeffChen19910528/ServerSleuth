using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Infrastructure.Process;

/// <summary>
/// Default IProcessRunner implementation. Always launches the executable directly with a
/// separate argument list (Process.ArgumentList) — never through cmd.exe/sh -c with an
/// interpolated string — so untrusted values passed as arguments cannot break out into shell
/// syntax. See skill.md §35.
/// </summary>
public sealed class ProcessRunner(ILogger<ProcessRunner> logger) : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = request.Executable,
                WorkingDirectory = request.WorkingDirectory ?? string.Empty,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        foreach (var argument in request.Arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in request.EnvironmentVariables)
        {
            process.StartInfo.Environment[key] = value;
        }

        try
        {
            process.Start();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2 || ex.NativeErrorCode == 3)
        {
            // ERROR_FILE_NOT_FOUND / ERROR_PATH_NOT_FOUND — command not available on this host.
            logger.LogWarning(ex, "Process executable not found: {Executable}", request.Executable);
            return ProcessResult.StartFailedResult(stopwatch.Elapsed) with { Status = OperationStatus.NotFound };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to start process: {Executable}", request.Executable);
            return ProcessResult.StartFailedResult(stopwatch.Elapsed);
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var exitTask = process.WaitForExitAsync(CancellationToken.None);

        using var timeoutCts = new CancellationTokenSource(request.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await exitTask.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);

            if (cancellationToken.IsCancellationRequested)
            {
                return ProcessResult.CancelledResult(stopwatch.Elapsed);
            }

            return ProcessResult.TimedOutResult(stopwatch.Elapsed);
        }

        var stdOut = await SafeReadAsync(stdOutTask);
        var stdErr = await SafeReadAsync(stdErrTask);

        return ProcessResult.Ok(process.ExitCode, stdOut, stdErr, stopwatch.Elapsed);
    }

    private static async Task<string> SafeReadAsync(Task<string> readTask)
    {
        try
        {
            return await readTask;
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
    }

    private void KillProcessTree(System.Diagnostics.Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to terminate process tree for {Executable}", process.StartInfo.FileName);
        }
    }
}
