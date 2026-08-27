using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;

namespace ServerSleuth.Core.Tests.Interfaces;

public class DiscoveryScannerTests
{
    [Fact]
    public async Task ScanAsync_ReturnsSupportedResultWithEntity()
    {
        var scanner = new FakeScanner();
        var context = new DiscoveryContext { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None };

        var result = await scanner.ScanAsync(context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Single(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_HonorsCancellation()
    {
        var scanner = new FakeScanner();
        var context = new DiscoveryContext { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => scanner.ScanAsync(context, cts.Token));
    }

    [Fact]
    public void PlatformSupport_ReflectsDeclaredFlags()
    {
        var scanner = new FakeScanner();

        Assert.True(scanner.PlatformSupport.HasFlag(PlatformSupport.Windows));
        Assert.True(scanner.PlatformSupport.HasFlag(PlatformSupport.Linux));
    }
}
