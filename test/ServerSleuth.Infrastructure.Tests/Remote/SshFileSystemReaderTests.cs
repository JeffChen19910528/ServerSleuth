using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Remote;
using ServerSleuth.Infrastructure.Tests.Remote.Fixtures;

namespace ServerSleuth.Infrastructure.Tests.Remote;

/// <summary>Phase 10D-2 §10: SFTP-backed <see cref="IFileSystem.IFileSystemReader"/>
/// equivalent — every member maps to a structured SFTP primitive via <see cref="ISshSession"/>.</summary>
public class SshFileSystemReaderTests
{
    [Fact]
    public async Task ReadTextAsync_ReturnsDecodedUtf8Content()
    {
        var session = new FakeSshSession();
        session.SetFile("/etc/os-release", "NAME=Ubuntu\n"u8.ToArray());

        var reader = new SshFileSystemReader(session);
        var result = await reader.ReadTextAsync("/etc/os-release", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("NAME=Ubuntu\n", result.Value);
    }

    [Fact]
    public async Task ReadTextAsync_MissingFile_ReturnsNotFound_NeverThrows()
    {
        var session = new FakeSshSession();
        var reader = new SshFileSystemReader(session);

        var result = await reader.ReadTextAsync("/does/not/exist", CancellationToken.None);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public void Exists_TrueForKnownFile()
    {
        var session = new FakeSshSession();
        session.SetFile("/etc/hosts", "127.0.0.1 localhost"u8.ToArray());

        var reader = new SshFileSystemReader(session);
        Assert.True(reader.Exists("/etc/hosts"));
    }

    [Fact]
    public void EnumerateDirectories_Recursive_IsRejected_NeverAFullRemoteWalk()
    {
        var session = new FakeSshSession();
        var reader = new SshFileSystemReader(session);

        var result = reader.EnumerateDirectories("/", "*", recursive: true);

        Assert.Equal(OperationStatus.InvalidInput, result.Status);
    }

    [Fact]
    public void EnumerateFiles_Recursive_IsRejected_NeverAFullRemoteWalk()
    {
        var session = new FakeSshSession();
        var reader = new SshFileSystemReader(session);

        var result = reader.EnumerateFiles("/", "*", recursive: true);

        Assert.Equal(OperationStatus.InvalidInput, result.Status);
    }

    [Fact]
    public void EnumerateDirectories_NonRecursive_ReturnsOnlyDirectoryEntries()
    {
        var session = new FakeSshSession();
        session.SetDirectory("/etc/systemd/system", [
            new SshRemoteFileInfo { FullPath = "/etc/systemd/system/nginx.service", IsDirectory = false },
            new SshRemoteFileInfo { FullPath = "/etc/systemd/system/multi-user.target.wants", IsDirectory = true }
        ]);

        var reader = new SshFileSystemReader(session);
        var result = reader.EnumerateDirectories("/etc/systemd/system");

        Assert.True(result.Success);
        Assert.Equal(["/etc/systemd/system/multi-user.target.wants"], result.Value);
    }

    [Fact]
    public void ReadLinkTarget_DelegatesToSession()
    {
        var session = new FakeSshSession();
        session.SetLinkTarget("/proc/1/exe", "/sbin/init");

        var reader = new SshFileSystemReader(session);
        var result = reader.ReadLinkTarget("/proc/1/exe");

        Assert.True(result.Success);
        Assert.Equal("/sbin/init", result.Value);
    }
}
