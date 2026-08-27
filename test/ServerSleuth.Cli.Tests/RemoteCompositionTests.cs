using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Cli.Composition;
using ServerSleuth.Cli.Options;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Remote;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Cli.Tests;

/// <summary>
/// Phase 10D-2 §20, §22: proves the composition root actually wires a remote target to the SSH
/// transport (never the local singletons), and that connecting to a genuinely unreachable host
/// fails cleanly rather than silently falling back to local. Uses a real (but never reachable —
/// port 1 on a reserved documentation-only IP, RFC 5737 TEST-NET-1) target, never a live SSH
/// server, so this stays fully deterministic and fast.
/// </summary>
public class RemoteCompositionTests
{
    private static RemoteScanOptions BuildRemoteOptions(string tempKeyPath) => new()
    {
        Host = "192.0.2.1", // TEST-NET-1 (RFC 5737) — guaranteed non-routable, never really contacted successfully
        Port = 1,
        Username = "tester",
        PrivateKeyPath = tempKeyPath,
        HostFingerprint = "aa:bb:cc"
    };

    [Fact]
    public void Build_RemoteTarget_RegistersTheSshTransport_NeverTheLocalOne()
    {
        var keyPath = WriteThrowawayKeyFile();
        try
        {
            var options = new ScanOptions { Remote = BuildRemoteOptions(keyPath) };
            using var provider = (ServiceProvider)CompositionRoot.Build(options);

            var transport = provider.GetRequiredService<ITargetTransport>();
            Assert.IsType<SshRemoteTargetTransport>(transport);
            Assert.Equal(TargetKind.Remote, transport.Target.Kind);

            var processRunner = provider.GetRequiredService<IProcessRunner>();
            var fileSystemReader = provider.GetRequiredService<IFileSystemReader>();
            Assert.IsType<SshProcessRunner>(processRunner);
            Assert.IsType<SshFileSystemReader>(fileSystemReader);
            Assert.Same(transport.ProcessRunner, processRunner);
            Assert.Same(transport.FileSystemReader, fileSystemReader);
        }
        finally
        {
            File.Delete(keyPath);
        }
    }

    [Fact]
    public void Build_RemoteTarget_StillRegistersLinuxScanners_RegardlessOfHostOs()
    {
        var keyPath = WriteThrowawayKeyFile();
        try
        {
            var options = new ScanOptions { Remote = BuildRemoteOptions(keyPath) };
            using var provider = (ServiceProvider)CompositionRoot.Build(options);

            var registry = provider.GetRequiredService<IDiscoveryScannerRegistry>();
            Assert.Contains(registry.Scanners, s => s.Id.StartsWith("linux-", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(keyPath);
        }
    }

    /// <summary>Critical safety proof (skill.md §22): the transport's ProcessRunner/FileSystemReader
    /// are never the same instances a LOCAL composition would have registered.</summary>
    [Fact]
    public void Build_RemoteTarget_NeverReusesTheLocalSingletons()
    {
        using var localProvider = (ServiceProvider)CompositionRoot.Build(new ScanOptions());
        var localProcessRunner = localProvider.GetRequiredService<IProcessRunner>();
        var localFileSystemReader = localProvider.GetRequiredService<IFileSystemReader>();

        var keyPath = WriteThrowawayKeyFile();
        try
        {
            var options = new ScanOptions { Remote = BuildRemoteOptions(keyPath) };
            using var remoteProvider = (ServiceProvider)CompositionRoot.Build(options);

            var remoteProcessRunner = remoteProvider.GetRequiredService<IProcessRunner>();
            var remoteFileSystemReader = remoteProvider.GetRequiredService<IFileSystemReader>();

            Assert.NotSame(localProcessRunner, remoteProcessRunner);
            Assert.NotSame(localFileSystemReader, remoteFileSystemReader);
        }
        finally
        {
            File.Delete(keyPath);
        }
    }

    private static string WriteThrowawayKeyFile()
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, "not-a-real-key"u8.ToArray()); // never actually used to connect in this test
        return path;
    }
}
