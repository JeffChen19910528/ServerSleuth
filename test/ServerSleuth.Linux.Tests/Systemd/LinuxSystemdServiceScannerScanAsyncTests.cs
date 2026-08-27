using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Linux.Systemd;

namespace ServerSleuth.Linux.Tests.Systemd;

public class LinuxSystemdServiceScannerScanAsyncTests
{
    private static DiscoveryContext Context() => new() { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None };

    private sealed class FakeSystemdProvider(SystemdProbeResult result) : ISystemdProvider
    {
        public SystemdProbeResult GetSnapshot() => result;
    }

    [Fact]
    public async Task ScanAsync_SystemdNotInstalled_ReturnsNotInstalled()
    {
        var scanner = new LinuxSystemdServiceScanner(
            new FakeSystemdProvider(new SystemdProbeResult { Status = SystemdAvailability.NotInstalled }),
            NullLogger<LinuxSystemdServiceScanner>.Instance);

        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
        Assert.Empty(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_AccessDenied_ReturnsAccessDeniedWithPermissionError()
    {
        var scanner = new LinuxSystemdServiceScanner(
            new FakeSystemdProvider(new SystemdProbeResult { Status = SystemdAvailability.AccessDenied, ErrorMessage = "denied" }),
            NullLogger<LinuxSystemdServiceScanner>.Instance);

        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.AccessDenied, result.Status);
        Assert.Single(result.Errors);
        Assert.True(result.Errors[0].IsPermissionFailure);
    }

    [Fact]
    public async Task ScanAsync_Failed_ReturnsFailed()
    {
        var scanner = new LinuxSystemdServiceScanner(
            new FakeSystemdProvider(new SystemdProbeResult { Status = SystemdAvailability.Failed, ErrorMessage = "boom" }),
            NullLogger<LinuxSystemdServiceScanner>.Instance);

        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Failed, result.Status);
    }

    [Fact]
    public async Task ScanAsync_MultipleServicesFullyResolved_ReturnsSupported()
    {
        var probe = new SystemdProbeResult
        {
            Status = SystemdAvailability.Available,
            Units =
            [
                new SystemdUnitRow { UnitName = "nginx.service", ActiveState = "active", LoadState = "loaded" },
                new SystemdUnitRow { UnitName = "cron.service", ActiveState = "active", LoadState = "loaded" }
            ]
        };

        var scanner = new LinuxSystemdServiceScanner(new FakeSystemdProvider(probe), NullLogger<LinuxSystemdServiceScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Equal(2, result.Entities.Count);
    }

    [Fact]
    public async Task ScanAsync_OneUnitDetailUnavailable_ReturnsPartiallySupported_NeverCrashes()
    {
        var probe = new SystemdProbeResult
        {
            Status = SystemdAvailability.Available,
            Units =
            [
                new SystemdUnitRow { UnitName = "nginx.service", ActiveState = "active", LoadState = "loaded" },
                new SystemdUnitRow { UnitName = "restricted.service", DetailUnavailable = true }
            ],
            PartialFailures = ["restricted.service: AccessDenied"]
        };

        var scanner = new LinuxSystemdServiceScanner(new FakeSystemdProvider(probe), NullLogger<LinuxSystemdServiceScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Equal(2, result.Entities.Count);
    }

    [Fact]
    public async Task ScanAsync_NoServices_ReturnsSupportedWithEmptyResult()
    {
        var scanner = new LinuxSystemdServiceScanner(
            new FakeSystemdProvider(new SystemdProbeResult { Status = SystemdAvailability.Available }),
            NullLogger<LinuxSystemdServiceScanner>.Instance);

        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Empty(result.Entities);
    }
}
