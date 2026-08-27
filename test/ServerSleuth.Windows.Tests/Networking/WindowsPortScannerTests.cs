using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Networking;
using ServerSleuth.Windows.Networking;

namespace ServerSleuth.Windows.Tests.Networking;

internal sealed class FakePortInspector(IReadOnlyList<NetworkEndpoint> endpoints) : IPortInspector
{
    public Task<IReadOnlyList<NetworkEndpoint>> GetListeningEndpointsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(endpoints);
}

internal sealed class ThrowingPortInspector : IPortInspector
{
    public Task<IReadOnlyList<NetworkEndpoint>> GetListeningEndpointsAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("simulated failure");
}

public class WindowsPortScannerTests
{
    [Fact]
    public void BuildEntity_MapsProtocolAddressPortAndOwningPid()
    {
        var endpoint = new NetworkEndpoint
        {
            Protocol = "TCP",
            LocalAddress = "0.0.0.0",
            LocalPort = 8011,
            ProcessId = 4532,
            ProcessName = "ERPService",
            State = "Listening"
        };

        var entity = WindowsPortScanner.BuildEntity(endpoint, 0);

        Assert.Equal("TCP", entity.Protocol);
        Assert.Equal("0.0.0.0", entity.LocalAddress);
        Assert.Equal(8011, entity.Number);
        Assert.Equal(4532, entity.OwningPid);
        Assert.Equal(EntityStatus.Listening, entity.Status);
        Assert.Equal("ERPService", entity.Metadata["ProcessName"]);
    }

    [Fact]
    public void BuildEntity_UnknownOwningProcessName_RecordsUnavailableNotEmptyString()
    {
        var endpoint = new NetworkEndpoint
        {
            Protocol = "UDP",
            LocalAddress = "::",
            LocalPort = 161,
            ProcessId = 999,
            ProcessName = null,
            State = "Listening"
        };

        var entity = WindowsPortScanner.BuildEntity(endpoint, 0);

        Assert.False(entity.Metadata.ContainsKey("ProcessName"));
        Assert.Equal("Unavailable", entity.Metadata["ProcessNameStatus"]);
    }

    [Fact]
    public async Task ScanAsync_ReturnsSupportedWithMappedEntities()
    {
        var endpoints = new List<NetworkEndpoint>
        {
            new() { Protocol = "TCP", LocalAddress = "0.0.0.0", LocalPort = 443, ProcessId = 4, ProcessName = "System", State = "Listening" }
        };

        var scanner = new WindowsPortScanner(new FakePortInspector(endpoints), NullLogger<WindowsPortScanner>.Instance);
        var result = await scanner.ScanAsync(new DiscoveryContext { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None }, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Single(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_InspectorThrows_ReturnsFailedWithoutPropagatingException()
    {
        var scanner = new WindowsPortScanner(new ThrowingPortInspector(), NullLogger<WindowsPortScanner>.Instance);
        var result = await scanner.ScanAsync(new DiscoveryContext { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None }, CancellationToken.None);

        Assert.Equal(ScannerStatus.Failed, result.Status);
        Assert.Single(result.Errors);
    }
}
