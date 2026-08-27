using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Linux.OperatingSystem;
using ServerSleuth.Linux.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace ServerSleuth.Linux.Tests.OperatingSystem;

public class LinuxOsScannerScanAsyncTests
{
    private static DiscoveryContext Context() => new() { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None };

    [Fact]
    public async Task ScanAsync_AllSourcesAvailable_ReturnsSupported()
    {
        var fs = new FakeFileSystemReader();
        fs.SetText("/etc/os-release", "PRETTY_NAME=\"Ubuntu 22.04\"\nID=ubuntu\nVERSION_ID=22.04");
        fs.SetText("/proc/sys/kernel/hostname", "web01\n");
        fs.SetText("/proc/sys/kernel/osrelease", "5.15.0-91-generic\n");
        fs.SetText("/proc/sys/kernel/ostype", "Linux\n");

        var processRunner = new FakeProcessRunner();
        processRunner.SetResult("uname", ["-m"], ProcessResult.Ok(0, "x86_64\n", "", TimeSpan.Zero));

        var scanner = new LinuxOsScanner(fs, processRunner, NullLogger<LinuxOsScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Equal(2, result.Entities.Count);
    }

    [Fact]
    public async Task ScanAsync_OsReleaseMissing_ReturnsPartiallySupportedButStillProducesEntities()
    {
        var fs = new FakeFileSystemReader();
        fs.SetTextFailure("/etc/os-release", OperationStatus.NotFound);
        fs.SetText("/proc/sys/kernel/hostname", "web01\n");
        fs.SetText("/proc/sys/kernel/osrelease", "5.15.0-91-generic\n");

        var processRunner = new FakeProcessRunner();

        var scanner = new LinuxOsScanner(fs, processRunner, NullLogger<LinuxOsScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.NotEmpty(result.Entities);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ScanAsync_UnameNotInstalled_StillCompletesWithoutArchitecture()
    {
        var fs = new FakeFileSystemReader();
        fs.SetText("/etc/os-release", "PRETTY_NAME=\"Ubuntu\"");

        var processRunner = new FakeProcessRunner(); // uname not registered -> StartFailedResult

        var scanner = new LinuxOsScanner(fs, processRunner, NullLogger<LinuxOsScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        var os = result.Entities.OfType<Core.Models.OperatingSystem>().Single();
        Assert.Equal(EntityArchitecture.Unknown, os.Architecture);
    }
}
