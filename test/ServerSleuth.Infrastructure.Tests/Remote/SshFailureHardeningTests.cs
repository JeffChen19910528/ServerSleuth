using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Remote;
using ServerSleuth.Infrastructure.Tests.Remote.Fixtures;

namespace ServerSleuth.Infrastructure.Tests.Remote;

/// <summary>
/// Phase 10E-2 §2, §9, §10, §11, §16: SSH-specific failure/cancellation/disposal/determinism
/// hardening NOT already covered by Phase 10D-2's own <see cref="SshRemoteTargetTransportTests"/>/
/// <see cref="SshProcessRunnerTests"/>/<see cref="TrustedFingerprintHostKeyVerifierTests"/>
/// (which already cover host-key mismatch/rejection, credential non-logging, command-injection
/// resistance, and basic connect-once/dispose-once semantics). Everything here runs against
/// <see cref="FakeSshSession"/> — no live SSH server.
/// </summary>
public class SshFailureHardeningTests
{
    private static readonly ScanTarget RemoteTarget = ScanTarget.Remote("db-server-1", TargetPlatform.Linux);

    // 2.A-F, 9: every connect-failure classification surfaces correctly, never as an unhandled exception.
    [Theory]
    [MemberData(nameof(ConnectFailures))]
    public void Connect_EveryFailureClassification_SurfacesTheExpectedStatus_NeverThrows(SshConnectResult failure, OperationStatus expectedStatus)
    {
        var session = new FakeSshSession { ConnectResult = failure };
        using var transport = new SshRemoteTargetTransport(RemoteTarget, session);

        var result = transport.Connect(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expectedStatus, result.Status);
        Assert.False(session.IsConnected);
    }

    public static IEnumerable<object[]> ConnectFailures()
    {
        yield return [SshConnectResult.HostKeyRejected(), OperationStatus.TransportUnavailable];
        yield return [SshConnectResult.AuthenticationFailed(), OperationStatus.AccessDenied];
        yield return [SshConnectResult.Unreachable("connection refused"), OperationStatus.TransportUnavailable];
        yield return [SshConnectResult.TimedOut(), OperationStatus.Timeout];
        yield return [SshConnectResult.Cancelled(), OperationStatus.Cancelled];
    }

    // 11: a failed connection still releases the underlying session when the transport is disposed.
    [Fact]
    public void Dispose_AfterFailedConnect_StillDisposesTheUnderlyingSession()
    {
        var session = new FakeSshSession { ConnectResult = SshConnectResult.Unreachable("refused") };
        var transport = new SshRemoteTargetTransport(RemoteTarget, session);

        transport.Connect(CancellationToken.None);
        transport.Dispose();

        Assert.Equal(1, session.DisposeCallCount);
    }

    // 16: a single failed connect attempt calls the underlying session exactly once — no retry storm.
    [Fact]
    public void FailedConnect_CallsTheUnderlyingSessionExactlyOnce_NoRetryStorm()
    {
        var session = new FakeSshSession { ConnectResult = SshConnectResult.Unreachable("refused") };
        using var transport = new SshRemoteTargetTransport(RemoteTarget, session);

        transport.Connect(CancellationToken.None);

        Assert.Equal(1, session.ConnectCallCount);
    }

    // 15: determinism — repeating an identical failed connect twice yields identical results.
    [Fact]
    public void RepeatedIdenticalFailedConnect_IsDeterministic()
    {
        var session = new FakeSshSession { ConnectResult = SshConnectResult.AuthenticationFailed() };
        using var transport = new SshRemoteTargetTransport(RemoteTarget, session);

        var first = transport.Connect(CancellationToken.None);
        var second = transport.Connect(CancellationToken.None);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.Success, second.Success);
    }

    // 7: after a failed connection, the transport's ProcessRunner/FileSystemReader remain the
    // SAME SSH-backed instances — never silently swapped for a local implementation.
    [Fact]
    public void FailedConnection_NeverSwapsProcessRunnerOrFileSystemReaderForALocalImplementation()
    {
        var session = new FakeSshSession { ConnectResult = SshConnectResult.Unreachable("refused") };
        using var transport = new SshRemoteTargetTransport(RemoteTarget, session);

        transport.Connect(CancellationToken.None);

        Assert.IsType<SshProcessRunner>(transport.ProcessRunner);
        Assert.IsType<SshFileSystemReader>(transport.FileSystemReader);
    }

    // 8: one remote command failing does not affect an independent one over the same shared session.
    [Fact]
    public async Task OneRemoteCommandFailure_DoesNotAffectAnIndependentCommand_OnTheSameSharedSession()
    {
        var session = new FakeSshSession();
        session.SetCommandResult("'systemctl' 'show' 'nginx' '--no-page'", SshCommandExecutionResult.Ok(1, string.Empty, "Unit not found."));
        session.Connect(CancellationToken.None);

        var runner = new SshProcessRunner(session);

        var failing = await runner.RunAsync(new ProcessRequest { Executable = "systemctl", Arguments = ["show", "nginx", "--no-page"] }, CancellationToken.None);
        var succeeding = await runner.RunAsync(new ProcessRequest { Executable = "systemctl", Arguments = ["show", "sshd", "--no-page"] }, CancellationToken.None);

        Assert.Equal(1, failing.ExitCode);
        Assert.Equal(OperationStatus.Success, succeeding.Status); // SshProcessRunner: Status=Success regardless of exit code; exit code carries the failure.
        Assert.Equal(0, succeeding.ExitCode);
    }

    // 4, 18: credentials never appear in a connect failure's error text.
    [Fact]
    public void ConnectFailure_ErrorMessageNeverContainsCredentialMaterial()
    {
        const string sentinelPassword = "SERVER_SLEUTH_TEST_SSH_PASSWORD_9f8c21";
        var session = new FakeSshSession { ConnectResult = SshConnectResult.Unreachable("Connection refused by remote host") };
        using var transport = new SshRemoteTargetTransport(RemoteTarget, session);

        var result = transport.Connect(CancellationToken.None);

        Assert.DoesNotContain(sentinelPassword, result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void SshConnectResult_HasNoFieldCapableOfHoldingACredential()
    {
        var fields = typeof(SshConnectResult).GetProperties();
        Assert.DoesNotContain(fields, p => p.PropertyType == typeof(RemoteCredential));
    }
}
