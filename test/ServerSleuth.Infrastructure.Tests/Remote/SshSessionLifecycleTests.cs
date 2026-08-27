using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Remote;
using ServerSleuth.Infrastructure.Tests.Remote.Fixtures;

namespace ServerSleuth.Infrastructure.Tests.Remote;

/// <summary>
/// Phase 10E-3 §A-B: session-reuse and lifecycle guarantees NOT already covered by Phase
/// 10D-2's/10E-2's own suites, which proved connect-once/dispose-once and no-retry-storm for a
/// SINGLE failing call. This suite proves the SAME session is reused across MULTIPLE, DIFFERENT
/// operations (the actual shape a real scan makes — one process query, one file read, one
/// directory listing, all against the same target), that a partial failure never disposes the
/// shared session out from under a later, independent operation, and that disposing the
/// transport twice is harmless.
/// </summary>
public class SshSessionLifecycleTests
{
    private static readonly ScanTarget RemoteTarget = ScanTarget.Remote("db-server-1", TargetPlatform.Linux);

    [Fact]
    public async Task MultipleDifferentOperations_AllReuseTheSameSession_NeverReconnect()
    {
        var session = new FakeSshSession();
        using var transport = new SshRemoteTargetTransport(RemoteTarget, session);
        transport.Connect(CancellationToken.None);
        Assert.Equal(1, session.ConnectCallCount);

        session.SetFile("/etc/os-release", "NAME=Ubuntu\n"u8.ToArray());
        session.SetDirectory("/etc", [new SshRemoteFileInfo { FullPath = "/etc/hosts", IsDirectory = false }]);

        // A process query, a file read, and a directory listing — the same mix a real Linux
        // scan issues — all through the transport's own ProcessRunner/FileSystemReader.
        await transport.ProcessRunner.RunAsync(new ProcessRequest { Executable = "uname", Arguments = ["-a"] }, CancellationToken.None);
        await transport.FileSystemReader.ReadTextAsync("/etc/os-release", CancellationToken.None);
        transport.FileSystemReader.EnumerateFiles("/etc");

        // Still exactly one connect — no hidden per-operation reconnect, no N+1 session creation.
        Assert.Equal(1, session.ConnectCallCount);
    }

    [Fact]
    public void MultipleProviderInstancesFromTheSameTransport_ShareTheIdenticalSessionInstance()
    {
        var session = new FakeSshSession();
        using var transport = new SshRemoteTargetTransport(RemoteTarget, session);

        var runnerA = transport.ProcessRunner;
        var runnerB = transport.ProcessRunner; // ITargetTransport exposes one instance, not a factory
        var readerA = transport.FileSystemReader;
        var readerB = transport.FileSystemReader;

        Assert.Same(runnerA, runnerB);
        Assert.Same(readerA, readerB);
    }

    // B: a partial (single-operation) failure never disposes or invalidates the shared session —
    // a later, independent operation on the same transport still succeeds normally.
    [Fact]
    public async Task OneFailedOperation_NeverDisposesOrInvalidatesTheSharedSession_LaterOperationsStillWork()
    {
        var session = new FakeSshSession();
        using var transport = new SshRemoteTargetTransport(RemoteTarget, session);
        transport.Connect(CancellationToken.None);

        // A file read for a path that was never registered — a clean NotFound failure.
        var missing = await transport.FileSystemReader.ReadTextAsync("/does/not/exist", CancellationToken.None);
        Assert.Equal(OperationStatus.NotFound, missing.Status);

        // The session itself is untouched by that failure — not disposed, still connected.
        Assert.Equal(0, session.DisposeCallCount);
        Assert.True(session.IsConnected);

        // A subsequent, independent, valid read on the SAME transport still succeeds.
        session.SetFile("/etc/hostname", "db-server-1\n"u8.ToArray());
        var succeeding = await transport.FileSystemReader.ReadTextAsync("/etc/hostname", CancellationToken.None);
        Assert.True(succeeding.Success);
    }

    // B: repeated disposal is harmless and deterministic.
    [Fact]
    public void DisposingTwice_IsHarmless_UnderlyingSessionDisposedTwice_NoException()
    {
        var session = new FakeSshSession();
        var transport = new SshRemoteTargetTransport(RemoteTarget, session);
        transport.Connect(CancellationToken.None);

        transport.Dispose();
        var exception = Record.Exception(transport.Dispose);

        Assert.Null(exception);
        Assert.Equal(2, session.DisposeCallCount); // both calls reached the underlying session — deterministic, not silently swallowed either.
    }
}
