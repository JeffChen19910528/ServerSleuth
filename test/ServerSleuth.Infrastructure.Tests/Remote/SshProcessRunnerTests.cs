using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Remote;
using ServerSleuth.Infrastructure.Tests.Remote.Fixtures;

namespace ServerSleuth.Infrastructure.Tests.Remote;

/// <summary>Phase 10D-2 §9: status mapping between the SSH-level result and the existing
/// <see cref="ProcessResult"/> shape every Linux provider already consumes.</summary>
public class SshProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_Success_MapsExitCodeAndOutput()
    {
        var session = new FakeSshSession { IsConnected = true };
        session.SetCommandResult("'systemctl' 'show' 'nginx.service'", SshCommandExecutionResult.Ok(0, "ActiveState=active", ""));

        var runner = new SshProcessRunner(session);
        var result = await runner.RunAsync(
            new ProcessRequest { Executable = "systemctl", Arguments = ["show", "nginx.service"] }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("ActiveState=active", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_NonZeroExit_StillStatusSuccess_ExitCodeCarriesTheFailure()
    {
        var session = new FakeSshSession { IsConnected = true };
        session.SetCommandResult("'dpkg-query' '-W'", SshCommandExecutionResult.Ok(1, "", "no packages found"));

        var runner = new SshProcessRunner(session);
        var result = await runner.RunAsync(new ProcessRequest { Executable = "dpkg-query", Arguments = ["-W"] }, CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(1, result.ExitCode);
        Assert.False(result.Success); // ProcessResult.Success requires ExitCode == 0 too
    }

    [Fact]
    public async Task RunAsync_NotConnected_ReturnsTransportUnavailable_NeverThrows()
    {
        var session = new FakeSshSession { IsConnected = false };
        var runner = new SshProcessRunner(session);

        var result = await runner.RunAsync(new ProcessRequest { Executable = "uname" }, CancellationToken.None);

        Assert.Equal(OperationStatus.TransportUnavailable, result.Status);
    }

    [Fact]
    public async Task RunAsync_RemoteTimeout_MapsToTimeoutStatus()
    {
        var session = new FakeSshSession { IsConnected = true };
        session.SetCommandResult("'slow-command'", SshCommandExecutionResult.TimedOut());

        var runner = new SshProcessRunner(session);
        var result = await runner.RunAsync(new ProcessRequest { Executable = "slow-command" }, CancellationToken.None);

        Assert.Equal(OperationStatus.Timeout, result.Status);
    }

    [Fact]
    public async Task RunAsync_Cancelled_MapsToCancelledStatus()
    {
        var session = new FakeSshSession { IsConnected = true };
        session.SetCommandResult("'cmd'", SshCommandExecutionResult.Cancelled());

        var runner = new SshProcessRunner(session);
        var result = await runner.RunAsync(new ProcessRequest { Executable = "cmd" }, CancellationToken.None);

        Assert.Equal(OperationStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task RunAsync_NeverBuildsARawShellString_ArgumentsAlwaysStayDiscreteUntilTheBuilder()
    {
        var session = new FakeSshSession { IsConnected = true };
        var runner = new SshProcessRunner(session);

        await runner.RunAsync(
            new ProcessRequest { Executable = "echo", Arguments = ["a;b", "c|d"] }, CancellationToken.None);

        // The command line the session actually receives is the ALREADY-safely-quoted output of
        // SshCommandLineBuilder — never the caller's raw "a;b"/"c|d" strings concatenated.
        Assert.Equal("'echo' 'a;b' 'c|d'", session.LastExecutedCommandLine);
    }
}
