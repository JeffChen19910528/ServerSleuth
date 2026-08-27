using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Remote;
using ServerSleuth.Infrastructure.Targets;
using ServerSleuth.Infrastructure.Tests.Remote.Fixtures;

namespace ServerSleuth.Infrastructure.Tests.Remote;

/// <summary>
/// Phase 10D-2 §22, the phase's own explicitly-required "critical safety test": a remote Linux
/// target must never be indistinguishable from — or accidentally fall back to — the local
/// machine.
/// </summary>
public class SshRemoteTargetTransportTests
{
    [Fact]
    public void Construction_RejectsALocalTarget_ThisTransportOnlyServesRemote()
    {
        var session = new FakeSshSession();
        Assert.Throws<InvalidOperationException>(() => new SshRemoteTargetTransport(ScanTarget.Local(), session));
    }

    [Fact]
    public void Transport_ExposesTheExactRemoteTargetItWasGiven_NeverASubstitutedLocalOne()
    {
        var remoteTarget = ScanTarget.Remote("db-server-1", TargetPlatform.Linux);
        var session = new FakeSshSession();
        using var transport = new SshRemoteTargetTransport(remoteTarget, session);

        Assert.Equal(remoteTarget, transport.Target);
        Assert.Equal(TargetKind.Remote, transport.Target.Kind);
        Assert.NotEqual(ScanTarget.LocalTargetId, transport.Target.Id);
    }

    /// <summary>The actual critical-safety assertion: the <see cref="IProcessRunner"/>/
    /// <see cref="IFileSystemReader"/> instances a remote transport hands to a scanner are NOT
    /// (and cannot be) the process-wide local singletons every scanner would otherwise receive
    /// from <c>AddServerSleuthInfrastructure()</c> — they are always fresh instances wrapping
    /// THIS transport's own <see cref="ISshSession"/>.</summary>
    [Fact]
    public void Transport_ProcessRunnerAndFileSystemReader_AreDistinctFromTheLocalSingletons()
    {
        var localProcessRunner = new ProcessRunner(Microsoft.Extensions.Logging.Abstractions.NullLogger<ProcessRunner>.Instance);
        var localFileSystemReader = new FileSystemReader();

        var remoteTarget = ScanTarget.Remote("db-server-1", TargetPlatform.Linux);
        using var transport = new SshRemoteTargetTransport(remoteTarget, new FakeSshSession());

        Assert.NotSame(localProcessRunner, transport.ProcessRunner);
        Assert.NotSame(localFileSystemReader, transport.FileSystemReader);
        Assert.IsType<SshProcessRunner>(transport.ProcessRunner);
        Assert.IsType<SshFileSystemReader>(transport.FileSystemReader);
    }

    [Fact]
    public void Transport_DoesNotConnect_UntilConnectIsCalledExplicitly()
    {
        var session = new FakeSshSession();
        using var transport = new SshRemoteTargetTransport(ScanTarget.Remote("host", TargetPlatform.Linux), session);

        Assert.Equal(0, session.ConnectCallCount);
        Assert.False(session.IsConnected);
    }

    [Fact]
    public void Connect_CallsTheUnderlyingSessionExactlyOnce()
    {
        var session = new FakeSshSession();
        using var transport = new SshRemoteTargetTransport(ScanTarget.Remote("host", TargetPlatform.Linux), session);

        transport.Connect(CancellationToken.None);

        Assert.Equal(1, session.ConnectCallCount);
        Assert.True(session.IsConnected);
    }

    [Fact]
    public void Dispose_DisposesTheUnderlyingSession()
    {
        var session = new FakeSshSession();
        var transport = new SshRemoteTargetTransport(ScanTarget.Remote("host", TargetPlatform.Linux), session);

        transport.Dispose();

        Assert.Equal(1, session.DisposeCallCount);
    }
}
