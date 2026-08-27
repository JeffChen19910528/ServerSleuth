using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Windows.Process;

namespace ServerSleuth.Windows.Tests.Process;

internal sealed class FakeProcessEnumerator(IReadOnlyList<ProcessSnapshot> snapshots) : IProcessEnumerator
{
    public IReadOnlyList<ProcessSnapshot> GetSnapshots() => snapshots;
}

internal sealed class FakeProcessWmiProvider(IReadOnlyDictionary<int, ProcessWmiInfo> byPid) : IProcessWmiProvider
{
    public IReadOnlyDictionary<int, ProcessWmiInfo> GetAll() => byPid;
}

public class WindowsProcessScannerScanAsyncTests
{
    [Fact]
    public async Task ScanAsync_AllProcessesFullyResolved_ReturnsSupported()
    {
        var snapshots = new List<ProcessSnapshot> { new() { Pid = 1, Name = "a" }, new() { Pid = 2, Name = "b" } };
        var wmi = new Dictionary<int, ProcessWmiInfo>
        {
            [1] = new() { ProcessId = 1, ExecutablePath = @"C:\a.exe" },
            [2] = new() { ProcessId = 2, ExecutablePath = @"C:\b.exe" }
        };

        var scanner = new WindowsProcessScanner(new FakeProcessEnumerator(snapshots), new FakeProcessWmiProvider(wmi), NullLogger<WindowsProcessScanner>.Instance);
        var result = await scanner.ScanAsync(new DiscoveryContext { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None }, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Equal(2, result.Entities.Count);
    }

    [Fact]
    public async Task ScanAsync_SomeProcessesUnresolved_ReturnsPartiallySupportedButKeepsAllEntities()
    {
        var snapshots = new List<ProcessSnapshot> { new() { Pid = 1, Name = "a" }, new() { Pid = 4, Name = "System" } };
        var wmi = new Dictionary<int, ProcessWmiInfo> { [1] = new() { ProcessId = 1, ExecutablePath = @"C:\a.exe" } };

        var scanner = new WindowsProcessScanner(new FakeProcessEnumerator(snapshots), new FakeProcessWmiProvider(wmi), NullLogger<WindowsProcessScanner>.Instance);
        var result = await scanner.ScanAsync(new DiscoveryContext { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None }, CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Equal(2, result.Entities.Count);
    }

    [Fact]
    public async Task ScanAsync_NoProcesses_ReturnsSupportedWithEmptyEntities()
    {
        var scanner = new WindowsProcessScanner(new FakeProcessEnumerator([]), new FakeProcessWmiProvider(new Dictionary<int, ProcessWmiInfo>()), NullLogger<WindowsProcessScanner>.Instance);
        var result = await scanner.ScanAsync(new DiscoveryContext { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None }, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Empty(result.Entities);
    }
}
