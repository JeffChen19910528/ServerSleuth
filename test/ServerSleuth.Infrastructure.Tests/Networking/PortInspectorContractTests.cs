using ServerSleuth.Infrastructure.Networking;

namespace ServerSleuth.Infrastructure.Tests.Networking;

public class PortInspectorContractTests
{
    [Fact]
    public async Task GetListeningEndpointsAsync_EmptyResult_ReturnsEmptyList()
    {
        var inspector = new FakePortInspector([]);

        var endpoints = await inspector.GetListeningEndpointsAsync(CancellationToken.None);

        Assert.Empty(endpoints);
    }

    [Fact]
    public async Task GetListeningEndpointsAsync_MultipleEndpoints_ReturnsAllWithCorrelatedProcess()
    {
        var expected = new List<NetworkEndpoint>
        {
            new() { Protocol = "TCP", LocalAddress = "0.0.0.0", LocalPort = 443, ProcessId = 4212, ProcessName = "w3wp", State = "Listening" },
            new() { Protocol = "TCP", LocalAddress = "::", LocalPort = 8011, ProcessId = 5501, ProcessName = "ERPService", State = "Listening" },
            new() { Protocol = "UDP", LocalAddress = "0.0.0.0", LocalPort = 161, ProcessId = null, ProcessName = null, State = "Listening" }
        };

        var inspector = new FakePortInspector(expected);

        var endpoints = await inspector.GetListeningEndpointsAsync(CancellationToken.None);

        Assert.Equal(3, endpoints.Count);
        Assert.Contains(endpoints, e => e.Protocol == "TCP" && e.LocalAddress == "::");
        Assert.Contains(endpoints, e => e.Protocol == "UDP" && e.ProcessId == null);
        Assert.Contains(endpoints, e => e.ProcessName == "ERPService" && e.LocalPort == 8011);
    }
}
