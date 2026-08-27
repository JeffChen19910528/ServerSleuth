using ServerSleuth.Linux.Networking;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Networking;

/// <summary>
/// The symlink-target-resolution branch (`FileInfo.LinkTarget` on `/proc/&lt;pid&gt;/fd/*`) can
/// only be meaningfully exercised on a real Linux host with real socket file descriptors — see
/// the Integration smoke test. These tests cover the bounded-enumeration and permission-handling
/// logic around it, which is fully fakeable.
/// </summary>
public class SocketOwnershipResolverTests
{
    [Fact]
    public void BuildInodeToPidMap_ProcUnavailable_ReturnsEmptyMap_NeverThrows()
    {
        var fs = new FakeFileSystemReader(); // /proc never registered

        var map = new SocketOwnershipResolver(fs).BuildInodeToPidMap();

        Assert.Empty(map);
    }

    [Fact]
    public void BuildInodeToPidMap_FdDirectoryAccessDeniedForOtherUsersProcess_IsSkippedNotFatal()
    {
        var fs = new FakeFileSystemReader();
        fs.SetDirectoryEntries("/proc", "/proc/1", "/proc/2");
        fs.SetDirectoryAccessDenied("/proc/1/fd");
        fs.SetFileEntries("/proc/2/fd"); // no fds at all — empty but readable

        var map = new SocketOwnershipResolver(fs).BuildInodeToPidMap();

        Assert.Empty(map); // neither pid contributed anything; no exception was thrown
    }

    [Fact]
    public void BuildInodeToPidMap_NonNumericProcEntries_AreSkipped()
    {
        var fs = new FakeFileSystemReader();
        fs.SetDirectoryEntries("/proc", "/proc/self", "/proc/sys", "/proc/version");

        var map = new SocketOwnershipResolver(fs).BuildInodeToPidMap();

        Assert.Empty(map);
    }
}
