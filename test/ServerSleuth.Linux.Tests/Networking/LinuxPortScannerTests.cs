using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Networking;
using ServerSleuth.Linux.Networking;

namespace ServerSleuth.Linux.Tests.Networking;

public class LinuxPortScannerTests
{
    private static DiscoveryContext Context() => new() { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None };

    private sealed class FakePortInspector(IReadOnlyList<NetworkEndpoint> endpoints) : IPortInspector
    {
        public Task<IReadOnlyList<NetworkEndpoint>> GetListeningEndpointsAsync(CancellationToken cancellationToken) => Task.FromResult(endpoints);
    }

    [Fact]
    public void BuildEntity_ResolvedOwnership_HasVeryHighConfidence()
    {
        var endpoint = new NetworkEndpoint { Protocol = "TCP", LocalAddress = "0.0.0.0", LocalPort = 443, ProcessId = 100, State = "Listening" };

        var entity = LinuxPortScanner.BuildEntity(endpoint, 0);

        Assert.Equal(100, entity.OwningPid);
        Assert.Equal(ConfidenceBand.VeryHigh, entity.Confidence.Band);
    }

    [Fact]
    public void BuildEntity_UnresolvedOwnership_NeverGuessesPid_RecordsMetadata()
    {
        var endpoint = new NetworkEndpoint { Protocol = "UDP", LocalAddress = "0.0.0.0", LocalPort = 53, ProcessId = null, State = "Listening" };

        var entity = LinuxPortScanner.BuildEntity(endpoint, 0);

        Assert.Null(entity.OwningPid);
        Assert.Equal("Unresolved", entity.Metadata["OwningPidStatus"]);
    }

    [Fact]
    public async Task ScanAsync_AllOwnershipResolved_ReturnsSupported()
    {
        var scanner = new LinuxPortScanner(
            new FakePortInspector([new NetworkEndpoint { Protocol = "TCP", LocalAddress = "0.0.0.0", LocalPort = 80, ProcessId = 1, State = "Listening" }]),
            NullLogger<LinuxPortScanner>.Instance);

        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
    }

    [Fact]
    public async Task ScanAsync_SomeOwnershipUnresolved_ReturnsPartiallySupported()
    {
        var scanner = new LinuxPortScanner(
            new FakePortInspector([new NetworkEndpoint { Protocol = "TCP", LocalAddress = "0.0.0.0", LocalPort = 80, ProcessId = null, State = "Listening" }]),
            NullLogger<LinuxPortScanner>.Instance);

        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
    }
}
