using ServerSleuth.Linux.Networking;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Networking;

public class LinuxPortInspectorTests
{
    private const string Header = "  sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode";

    // Exactly 10 whitespace-separated fields, matching proc(5)'s real /proc/net/tcp shape:
    // sl local_address rem_address st tx_queue:rx_queue tr:tm->when retrnsmt uid timeout inode
    private static string Row(string localAddressHex, string stateHex, string inode) =>
        $"   0: {localAddressHex} 00000000:0000 {stateHex} 00000000:00000000 00:00000000 00000000 0 0 {inode}";

    private sealed class FakeOwnershipResolver(IReadOnlyDictionary<string, int> map) : ISocketOwnershipResolver
    {
        public IReadOnlyDictionary<string, int> BuildInodeToPidMap() => map;
    }

    [Fact]
    public async Task GetListeningEndpointsAsync_TcpListenRow_IsIncluded_TcpEstablishedRow_IsExcluded()
    {
        var fs = new FakeFileSystemReader();
        fs.SetText("/proc/net/tcp", Header +
            "\n" + Row("0100007F:1F90", "0A", "111") +
            "\n" + Row("0100007F:0016", "01", "112")); // 01 = ESTABLISHED, must be excluded
        fs.SetTextFailure("/proc/net/tcp6", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);
        fs.SetTextFailure("/proc/net/udp", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);
        fs.SetTextFailure("/proc/net/udp6", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);

        var inspector = new LinuxPortInspector(fs, new FakeOwnershipResolver(new Dictionary<string, int>()));
        var endpoints = await inspector.GetListeningEndpointsAsync(CancellationToken.None);

        var endpoint = Assert.Single(endpoints);
        Assert.Equal(8080, endpoint.LocalPort);
        Assert.Equal("TCP", endpoint.Protocol);
    }

    [Fact]
    public async Task GetListeningEndpointsAsync_UdpRow_IsAlwaysIncludedRegardlessOfState()
    {
        var fs = new FakeFileSystemReader();
        fs.SetTextFailure("/proc/net/tcp", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);
        fs.SetTextFailure("/proc/net/tcp6", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);
        fs.SetText("/proc/net/udp", Header + "\n" + Row("00000000:0035", "07", "5555"));
        fs.SetTextFailure("/proc/net/udp6", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);

        var inspector = new LinuxPortInspector(fs, new FakeOwnershipResolver(new Dictionary<string, int>()));
        var endpoints = await inspector.GetListeningEndpointsAsync(CancellationToken.None);

        var endpoint = Assert.Single(endpoints);
        Assert.Equal("UDP", endpoint.Protocol);
        Assert.Equal(53, endpoint.LocalPort);
    }

    [Fact]
    public async Task GetListeningEndpointsAsync_InodeResolvesToPid_SetsProcessId()
    {
        var fs = new FakeFileSystemReader();
        fs.SetText("/proc/net/tcp", Header + "\n" + Row("0100007F:1F90", "0A", "4242"));
        fs.SetTextFailure("/proc/net/tcp6", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);
        fs.SetTextFailure("/proc/net/udp", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);
        fs.SetTextFailure("/proc/net/udp6", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);

        var inspector = new LinuxPortInspector(fs, new FakeOwnershipResolver(new Dictionary<string, int> { ["4242"] = 777 }));
        var endpoints = await inspector.GetListeningEndpointsAsync(CancellationToken.None);

        var endpoint = Assert.Single(endpoints);
        Assert.Equal(777, endpoint.ProcessId);
    }

    [Fact]
    public async Task GetListeningEndpointsAsync_InodeNotResolvable_LeavesProcessIdNull_NeverGuesses()
    {
        var fs = new FakeFileSystemReader();
        fs.SetText("/proc/net/tcp", Header + "\n" + Row("0100007F:1F90", "0A", "9999"));
        fs.SetTextFailure("/proc/net/tcp6", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);
        fs.SetTextFailure("/proc/net/udp", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);
        fs.SetTextFailure("/proc/net/udp6", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);

        var inspector = new LinuxPortInspector(fs, new FakeOwnershipResolver(new Dictionary<string, int>()));
        var endpoints = await inspector.GetListeningEndpointsAsync(CancellationToken.None);

        var endpoint = Assert.Single(endpoints);
        Assert.Null(endpoint.ProcessId);
    }

    [Fact]
    public async Task GetListeningEndpointsAsync_Ipv6SourcesUnavailable_DoesNotFailTheWholeInspection()
    {
        var fs = new FakeFileSystemReader();
        fs.SetText("/proc/net/tcp", Header + "\n" + Row("0100007F:1F90", "0A", "1"));
        fs.SetTextFailure("/proc/net/tcp6", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound); // IPv6 disabled
        fs.SetTextFailure("/proc/net/udp", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);
        fs.SetTextFailure("/proc/net/udp6", ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);

        var inspector = new LinuxPortInspector(fs, new FakeOwnershipResolver(new Dictionary<string, int>()));
        var endpoints = await inspector.GetListeningEndpointsAsync(CancellationToken.None);

        Assert.Single(endpoints);
    }
}
