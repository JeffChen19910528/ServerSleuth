using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Infrastructure.Tests.Process;

/// <summary>
/// ProcessRunner is cross-platform infrastructure, but the concrete fixtures used here
/// (cmd.exe, ping) are Windows-only, matching the only OS this test suite currently runs on.
/// Each Windows-only test exits early (no assertion) when not running on Windows rather than
/// failing, so the suite still passes on a future Linux CI runner without producing a false
/// failure — equivalent Linux fixtures (sh, sleep) belong with Phase 6.
/// </summary>
public class ProcessRunnerTests
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private readonly ProcessRunner _runner = new(NullLogger<ProcessRunner>.Instance);

    [Fact]
    public async Task RunAsync_SuccessfulProcess_CapturesExitCodeAndStdOut()
    {
        if (!IsWindows) return;

        var request = new ProcessRequest
        {
            Executable = "cmd.exe",
            Arguments = ["/c", "echo", "hello-from-test"]
        };

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello-from-test", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_NonZeroExitCode_IsCapturedWithoutThrowing()
    {
        if (!IsWindows) return;

        var request = new ProcessRequest
        {
            Executable = "cmd.exe",
            Arguments = ["/c", "exit", "3"]
        };

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(3, result.ExitCode);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RunAsync_CapturesStandardError()
    {
        if (!IsWindows) return;

        var request = new ProcessRequest
        {
            Executable = "cmd.exe",
            Arguments = ["/c", "echo", "err-message", "1>&2"]
        };

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.Contains("err-message", result.StandardError);
    }

    [Fact]
    public async Task RunAsync_InvalidArguments_ProducesNonZeroExitAndStderr()
    {
        if (!IsWindows) return;

        var request = new ProcessRequest
        {
            Executable = "cmd.exe",
            Arguments = ["/c", "dir", @"Z:\this\path\definitely\does\not\exist\abc123"]
        };

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_ExceedsTimeout_ReturnsTimedOut()
    {
        if (!IsWindows) return;

        var request = new ProcessRequest
        {
            Executable = "ping",
            Arguments = ["127.0.0.1", "-n", "20"],
            Timeout = TimeSpan.FromMilliseconds(500)
        };

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.Equal(OperationStatus.Timeout, result.Status);
        Assert.True(result.TimedOut);
        Assert.False(result.Cancelled);
        Assert.True(result.Duration < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RunAsync_CancelledBeforeCompletion_ReturnsCancelled()
    {
        if (!IsWindows) return;

        var request = new ProcessRequest
        {
            Executable = "ping",
            Arguments = ["127.0.0.1", "-n", "20"],
            Timeout = TimeSpan.FromSeconds(30)
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(300));

        var result = await _runner.RunAsync(request, cts.Token);

        Assert.Equal(OperationStatus.Cancelled, result.Status);
        Assert.True(result.Cancelled);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_ExecutableNotFound_ReturnsNotFoundWithoutThrowing()
    {
        var request = new ProcessRequest
        {
            Executable = "this-executable-definitely-does-not-exist-12345.exe"
        };

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.Equal(OperationStatus.NotFound, result.Status);
        Assert.Null(result.ExitCode);
    }
}
