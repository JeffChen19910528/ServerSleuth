using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Remote;
using ServerSleuth.Linux.Process;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Process;

/// <summary>Exercises the real `LinuxProcProvider` against a fake `IFileSystemReader` (real
/// `/proc` doesn't exist on the Windows dev machine). Phase 10D-2: `/proc/&lt;pid&gt;/exe`
/// symlink resolution now goes entirely through `IFileSystemReader.ReadLinkTarget` (no direct
/// `FileInfo.LinkTarget` call on the local disk any more), so it IS fully exercised here —
/// including against a remote target once `SshFileSystemReader` implements the same
/// interface.</summary>
public class LinuxProcProviderTests
{
    [Fact]
    public void GetProcessSnapshots_ExeSymlinkResolves_ViaFileSystemReader_NeverTheLocalDiskDirectly()
    {
        var fs = new FakeFileSystemReader();
        fs.SetDirectoryEntries("/proc", "/proc/1");
        fs.SetText("/proc/1/status", "Name:\tinit\nState:\tS\nPPid:\t0\nUid:\t0\t0\t0\t0\n");
        fs.SetText("/proc/1/cmdline", "/sbin/init\0");
        fs.SetLinkTarget("/proc/1/exe", "/sbin/init");

        var snapshot = new LinuxProcProvider(fs).GetProcessSnapshots().Single();

        Assert.Equal("/sbin/init", snapshot.ExecutablePath);
    }

    [Fact]
    public void GetProcessSnapshots_ExeSymlinkUnresolvable_ProducesNullExecutablePath_NeverThrows()
    {
        var fs = new FakeFileSystemReader();
        fs.SetDirectoryEntries("/proc", "/proc/2");
        fs.SetText("/proc/2/status", "Name:\tkthreadd\nState:\tS\nPPid:\t0\n");
        fs.SetText("/proc/2/cmdline", string.Empty);
        // No SetLinkTarget call — a kernel thread has no /proc/<pid>/exe target.

        var snapshot = new LinuxProcProvider(fs).GetProcessSnapshots().Single();

        Assert.Null(snapshot.ExecutablePath);
    }

    [Fact]
    public void GetProcessSnapshots_TwoProcesses_ParsesStatusAndCmdline()
    {
        var fs = new FakeFileSystemReader();
        fs.SetDirectoryEntries("/proc", "/proc/1", "/proc/42", "/proc/self", "/proc/sys");
        fs.SetText("/proc/1/status", "Name:\tinit\nState:\tS (sleeping)\nPPid:\t0\nUid:\t0\t0\t0\t0\n");
        fs.SetText("/proc/1/cmdline", "/sbin/init\0");
        fs.SetText("/proc/42/status", "Name:\tnginx\nState:\tS (sleeping)\nPPid:\t1\nUid:\t33\t33\t33\t33\n");
        fs.SetText("/proc/42/cmdline", "/usr/sbin/nginx\0-g\0daemon off;\0");

        var provider = new LinuxProcProvider(fs);
        var snapshots = provider.GetProcessSnapshots();

        Assert.Equal(2, snapshots.Count); // "self" and "sys" are correctly skipped (non-numeric)
        var init = snapshots.Single(s => s.Pid == 1);
        Assert.Equal("init", init.Name);
        Assert.Equal("/sbin/init", init.CommandLine);
        var nginx = snapshots.Single(s => s.Pid == 42);
        Assert.Equal("nginx", nginx.Name);
        Assert.Equal("/usr/sbin/nginx -g daemon off;", nginx.CommandLine);
        Assert.Equal("33", nginx.Uid);
    }

    [Fact]
    public void GetProcessSnapshots_StatusAccessDenied_ProducesAccessDeniedSnapshot_NeverThrows()
    {
        var fs = new FakeFileSystemReader();
        fs.SetDirectoryEntries("/proc", "/proc/999");
        fs.SetTextFailure("/proc/999/status", OperationStatus.AccessDenied);

        var snapshots = new LinuxProcProvider(fs).GetProcessSnapshots();

        var snapshot = Assert.Single(snapshots);
        Assert.True(snapshot.AccessDenied);
    }

    [Fact]
    public void GetProcessSnapshots_ProcessVanishesBetweenListingAndRead_ProducesNotFoundLikeSnapshot_NeverThrows()
    {
        var fs = new FakeFileSystemReader();
        fs.SetDirectoryEntries("/proc", "/proc/12345");
        fs.SetTextFailure("/proc/12345/status", OperationStatus.NotFound); // exited mid-scan

        var snapshots = new LinuxProcProvider(fs).GetProcessSnapshots();

        var snapshot = Assert.Single(snapshots);
        Assert.False(snapshot.AccessDenied);
        Assert.Null(snapshot.Name);
    }

    [Fact]
    public void GetProcessSnapshots_StatusWithNoNameField_IsMarkedMalformed()
    {
        var fs = new FakeFileSystemReader();
        fs.SetDirectoryEntries("/proc", "/proc/7");
        fs.SetText("/proc/7/status", "State:\tS\nPPid:\t1\n"); // missing Name: entirely

        var snapshots = new LinuxProcProvider(fs).GetProcessSnapshots();

        var snapshot = Assert.Single(snapshots);
        Assert.True(snapshot.MalformedEntry);
    }

    [Fact]
    public void GetProcessSnapshots_ProcDirectoryUnavailable_ReturnsEmptyList()
    {
        var fs = new FakeFileSystemReader(); // /proc never registered

        var snapshots = new LinuxProcProvider(fs).GetProcessSnapshots();

        Assert.Empty(snapshots);
    }

    /// <summary>Phase 10D-2 §21: reruns this same provider against the SSH-backed
    /// <see cref="SshFileSystemReader"/> instead of the plain in-memory fake above, proving
    /// <see cref="LinuxProcProvider"/> is remote-capable with ZERO changes to the provider
    /// itself — exactly skill.md §21's goal.</summary>
    [Fact]
    public void GetProcessSnapshots_WorksUnmodified_AgainstTheRemoteSshFileSystemReader()
    {
        var session = new MinimalFakeSshSession();
        session.Directories["/proc"] = [new SshRemoteFileInfo { FullPath = "/proc/1", IsDirectory = true }];
        session.Files["/proc/1/status"] = "Name:\tinit\nState:\tS\nPPid:\t0\nUid:\t0\t0\t0\t0\n"u8.ToArray();
        session.Files["/proc/1/cmdline"] = "/sbin/init\0"u8.ToArray();
        session.LinkTargets["/proc/1/exe"] = "/sbin/init";

        var provider = new LinuxProcProvider(new SshFileSystemReader(session));
        var snapshot = provider.GetProcessSnapshots().Single();

        Assert.Equal("init", snapshot.Name);
        Assert.Equal("/sbin/init", snapshot.ExecutablePath);
    }

    [Fact]
    public void GetProcessSnapshots_KernelThreadWithEmptyCmdline_CommandLineIsNull()
    {
        var fs = new FakeFileSystemReader();
        fs.SetDirectoryEntries("/proc", "/proc/2");
        fs.SetText("/proc/2/status", "Name:\tkthreadd\nState:\tS\nPPid:\t0\n");
        fs.SetText("/proc/2/cmdline", string.Empty); // kernel threads have an empty cmdline

        var snapshots = new LinuxProcProvider(fs).GetProcessSnapshots();

        var snapshot = Assert.Single(snapshots);
        Assert.Null(snapshot.CommandLine);
    }

    /// <summary>The minimum <c>ISshSession</c> double needed to exercise
    /// <see cref="SshFileSystemReader"/> from this test — deliberately not shared with
    /// <c>ServerSleuth.Infrastructure.Tests</c>'s own richer <c>FakeSshSession</c>, since
    /// <c>ServerSleuth.Linux.Tests</c> has no reason to reference Infrastructure's test
    /// assembly.</summary>
    private sealed class MinimalFakeSshSession : ServerSleuth.Infrastructure.Remote.ISshSession
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, IReadOnlyList<SshRemoteFileInfo>> Directories { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> LinkTargets { get; } = new(StringComparer.Ordinal);

        public bool IsConnected => true;

        public ServerSleuth.Infrastructure.Remote.SshConnectResult Connect(CancellationToken cancellationToken) =>
            ServerSleuth.Infrastructure.Remote.SshConnectResult.Ok();

        public ServerSleuth.Infrastructure.Remote.SshCommandExecutionResult ExecuteCommand(string commandLine, TimeSpan timeout, CancellationToken cancellationToken) =>
            ServerSleuth.Infrastructure.Remote.SshCommandExecutionResult.Ok(0, string.Empty, string.Empty);

        public bool SftpExists(string path) => Files.ContainsKey(path);

        public ServerSleuth.Infrastructure.FileSystem.FileSystemResult<byte[]> SftpReadBytes(string path) =>
            Files.TryGetValue(path, out var bytes)
                ? ServerSleuth.Infrastructure.FileSystem.FileSystemResult<byte[]>.Ok(bytes)
                : ServerSleuth.Infrastructure.FileSystem.FileSystemResult<byte[]>.Failure(OperationStatus.NotFound, "not found");

        public ServerSleuth.Infrastructure.FileSystem.FileSystemResult<SshRemoteFileInfo> SftpGetAttributes(string path) =>
            ServerSleuth.Infrastructure.FileSystem.FileSystemResult<SshRemoteFileInfo>.Failure(OperationStatus.NotFound, "not implemented in fake");

        public ServerSleuth.Infrastructure.FileSystem.FileSystemResult<string> ReadLinkTarget(string path) =>
            LinkTargets.TryGetValue(path, out var target)
                ? ServerSleuth.Infrastructure.FileSystem.FileSystemResult<string>.Ok(target)
                : ServerSleuth.Infrastructure.FileSystem.FileSystemResult<string>.Failure(OperationStatus.NotFound, "not a symlink");

        public ServerSleuth.Infrastructure.FileSystem.FileSystemResult<IReadOnlyList<SshRemoteFileInfo>> SftpListDirectory(string path) =>
            Directories.TryGetValue(path, out var entries)
                ? ServerSleuth.Infrastructure.FileSystem.FileSystemResult<IReadOnlyList<SshRemoteFileInfo>>.Ok(entries)
                : ServerSleuth.Infrastructure.FileSystem.FileSystemResult<IReadOnlyList<SshRemoteFileInfo>>.Failure(OperationStatus.NotFound, "not found");

        public void Dispose()
        {
        }
    }
}
