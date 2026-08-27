using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Linux.Process;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Process;

public class LinuxProcessScannerScanAsyncTests
{
    private static DiscoveryContext Context() => new() { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None };

    [Fact]
    public async Task ScanAsync_AllProcessesFullyResolved_ReturnsSupported()
    {
        var provider = new FakeProcProvider([
            new ProcProcessSnapshot { Pid = 1, Name = "init", ExecutablePath = "/sbin/init" },
            new ProcProcessSnapshot { Pid = 2, Name = "nginx", ExecutablePath = "/usr/sbin/nginx" }
        ]);

        var scanner = new LinuxProcessScanner(provider, NullLogger<LinuxProcessScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Equal(2, result.Entities.Count);
    }

    [Fact]
    public async Task ScanAsync_OneProcessAccessDenied_ReturnsPartiallySupported_NeverCrashes()
    {
        var provider = new FakeProcProvider([
            new ProcProcessSnapshot { Pid = 1, Name = "init", ExecutablePath = "/sbin/init" },
            new ProcProcessSnapshot { Pid = 999, AccessDenied = true }
        ]);

        var scanner = new LinuxProcessScanner(provider, NullLogger<LinuxProcessScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Equal(2, result.Entities.Count);
    }

    [Fact]
    public async Task ScanAsync_ZombieProcess_NoExecutablePath_StillProducesEntityAsPartial()
    {
        var provider = new FakeProcProvider([
            new ProcProcessSnapshot { Pid = 500, Name = "defunct-proc", State = "Z (zombie)", ExecutablePath = null }
        ]);

        var scanner = new LinuxProcessScanner(provider, NullLogger<LinuxProcessScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Single(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_NoProcesses_ReturnsSupportedWithEmptyResult()
    {
        var provider = new FakeProcProvider([]);
        var scanner = new LinuxProcessScanner(provider, NullLogger<LinuxProcessScanner>.Instance);

        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Empty(result.Entities);
    }
}
