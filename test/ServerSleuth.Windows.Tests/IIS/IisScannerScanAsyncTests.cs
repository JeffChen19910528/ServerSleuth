using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Windows.IIS;

namespace ServerSleuth.Windows.Tests.IIS;

internal sealed class FakeIisConfigurationProvider(IisProbeResult result) : IIisConfigurationProvider
{
    public IisProbeResult GetSnapshot() => result;
}

public class IisScannerScanAsyncTests
{
    private static readonly DiscoveryContext Context = new() { Profile = ScanProfile.Standard, CancellationToken = CancellationToken.None };

    [Fact]
    public async Task ScanAsync_IisNotInstalled_ReturnsNotInstalledWithZeroEntitiesNotFailed()
    {
        var scanner = new IisScanner(new FakeIisConfigurationProvider(IisProbeResult.NotInstalled()), new FileSystemReader(), NullLogger<IisScanner>.Instance);

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
        Assert.Empty(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_AccessDenied_ReturnsAccessDeniedNotFailed()
    {
        var scanner = new IisScanner(
            new FakeIisConfigurationProvider(IisProbeResult.Failure(IisAvailability.AccessDenied, "denied")),
            new FileSystemReader(),
            NullLogger<IisScanner>.Instance);

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.AccessDenied, result.Status);
        Assert.Single(result.Errors);
        Assert.True(result.Errors[0].IsPermissionFailure);
    }

    [Fact]
    public async Task ScanAsync_UnexpectedFailure_ReturnsFailedWithError()
    {
        var scanner = new IisScanner(
            new FakeIisConfigurationProvider(IisProbeResult.Failure(IisAvailability.Failed, "boom")),
            new FileSystemReader(),
            NullLogger<IisScanner>.Instance);

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Failed, result.Status);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ScanAsync_AvailableWithNoPartialFailures_ReturnsSupported()
    {
        var snapshot = new IisSnapshot { Sites = [new IisSiteRow { Name = "ERP", SiteId = 1, State = "Started" }] };
        var scanner = new IisScanner(
            new FakeIisConfigurationProvider(IisProbeResult.Available(snapshot)),
            new FileSystemReader(),
            NullLogger<IisScanner>.Instance);

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Single(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_AvailableWithPartialFailures_ReturnsPartiallySupportedButKeepsGoodEntities()
    {
        var snapshot = new IisSnapshot { Sites = [new IisSiteRow { Name = "ERP", SiteId = 1, State = "Started" }] };
        var scanner = new IisScanner(
            new FakeIisConfigurationProvider(IisProbeResult.Available(snapshot, ["Site 'Broken': access denied"])),
            new FileSystemReader(),
            NullLogger<IisScanner>.Instance);

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Single(result.Entities);
        Assert.Single(result.Errors);
    }
}
