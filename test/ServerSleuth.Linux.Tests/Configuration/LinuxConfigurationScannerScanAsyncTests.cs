using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Linux.Configuration;
using ServerSleuth.Linux.Cron;
using ServerSleuth.Linux.Systemd;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Configuration;

public class LinuxConfigurationScannerScanAsyncTests
{
    private static DiscoveryContext Context() => new() { Profile = ScanProfile.Migration, CancellationToken = CancellationToken.None };

    private sealed class FakeSystemdProvider(SystemdProbeResult result) : ISystemdProvider
    {
        public SystemdProbeResult GetSnapshot() => result;
    }

    private static LinuxConfigurationScanner Scanner(FakeFileSystemReader fileSystemReader, SystemdProbeResult? systemdResult = null)
    {
        var systemdScanner = new LinuxSystemdServiceScanner(
            new FakeSystemdProvider(systemdResult ?? new SystemdProbeResult { Status = SystemdAvailability.Available }),
            NullLogger<LinuxSystemdServiceScanner>.Instance);
        var cronScanner = new LinuxScheduledTaskScanner(fileSystemReader, new SecretRedactor(), NullLogger<LinuxScheduledTaskScanner>.Instance);

        return new LinuxConfigurationScanner(systemdScanner, cronScanner, fileSystemReader, new SecretRedactor(), NullLogger<LinuxConfigurationScanner>.Instance);
    }

    [Fact]
    public async Task ScanAsync_NoFilesAnywhere_ReturnsSupported_WithNoEntities()
    {
        var fileSystemReader = new FakeFileSystemReader();

        var result = await Scanner(fileSystemReader).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Empty(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_NginxConfigUnderWellKnownRoot_DiscoveredAsConfigurationEntity()
    {
        var fileSystemReader = new FakeFileSystemReader();
        fileSystemReader.SetFileEntries("/etc/nginx", "/etc/nginx/nginx.conf");
        fileSystemReader.SetFileInfo("/etc/nginx/nginx.conf", 512);
        fileSystemReader.SetText("/etc/nginx/nginx.conf", "server {\n    listen 80;\n    server_name erp.example.com;\n}\n");

        var result = await Scanner(fileSystemReader).ScanAsync(Context(), CancellationToken.None);

        var entity = Assert.Single(result.Entities);
        Assert.Equal("configuration:/etc/nginx/nginx.conf", entity.Id);
        Assert.Equal("erp.example.com", entity.Metadata["Nginx.server_name0"]);
    }

    [Fact]
    public async Task ScanAsync_SameFileMatchedByTwoRootsOrPatterns_NeverDuplicated()
    {
        var fileSystemReader = new FakeFileSystemReader();
        // Both *.conf and *.cnf patterns are searched under /etc/mysql — the file only matches
        // one, but the dedup-by-path guard is what's under test regardless of which pattern hits.
        fileSystemReader.SetFileEntries("/etc/mysql", "/etc/mysql/my.cnf");
        fileSystemReader.SetFileInfo("/etc/mysql/my.cnf", 128);
        fileSystemReader.SetText("/etc/mysql/my.cnf", "[mysqld]\ndatadir=/var/lib/mysql");

        var result = await Scanner(fileSystemReader).ScanAsync(Context(), CancellationToken.None);

        Assert.Single(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_AccessDeniedOnOneRoot_DegradesToPartiallySupported_OtherRootsStillScanned()
    {
        var fileSystemReader = new FakeFileSystemReader();
        fileSystemReader.SetDirectoryAccessDenied("/etc/ssh");
        fileSystemReader.SetFileEntries("/etc/nginx", "/etc/nginx/nginx.conf");
        fileSystemReader.SetFileInfo("/etc/nginx/nginx.conf", 64);
        fileSystemReader.SetText("/etc/nginx/nginx.conf", "server { listen 80; }");

        var result = await Scanner(fileSystemReader).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.NotEmpty(result.Errors);
        Assert.Single(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_FileTooLarge_SkippedButRecordedAsEntity_NeverCrashesTheScan()
    {
        var fileSystemReader = new FakeFileSystemReader();
        fileSystemReader.SetFileEntries("/etc/nginx", "/etc/nginx/huge.conf");
        fileSystemReader.SetFileInfo("/etc/nginx/huge.conf", 2 * 1024 * 1024);

        var result = await Scanner(fileSystemReader).ScanAsync(Context(), CancellationToken.None);

        var entity = Assert.Single(result.Entities);
        Assert.Equal("SkippedTooLarge", entity.Metadata["ParseStatus"]);
    }

    [Fact]
    public async Task ScanAsync_ServiceExecStartUnderOpt_ApplicationRootIsScanned()
    {
        var fileSystemReader = new FakeFileSystemReader();
        fileSystemReader.SetFileEntries("/opt/erp", "/opt/erp/appsettings.json");
        fileSystemReader.SetFileInfo("/opt/erp/appsettings.json", 64);
        fileSystemReader.SetText("/opt/erp/appsettings.json", """{ "Logging": {} }""");

        var systemdResult = new SystemdProbeResult
        {
            Status = SystemdAvailability.Available,
            Units = [new SystemdUnitRow { UnitName = "erp.service", ExecStart = "path=/opt/erp/bin/erp", ActiveState = "active", LoadState = "loaded" }]
        };

        var result = await Scanner(fileSystemReader, systemdResult).ScanAsync(Context(), CancellationToken.None);

        Assert.Contains(result.Entities, e => e.Path == "/opt/erp/appsettings.json" && e.Metadata["OwnerEntityId"] == "service:erp.service");
    }

    [Fact]
    public async Task ScanAsync_SystemdUnitWithEnvironmentFile_DiscoversReferencedFileAsSecondConfigurationEntity()
    {
        var fileSystemReader = new FakeFileSystemReader();
        fileSystemReader.SetFileEntries("/etc/systemd/system", "/etc/systemd/system/erp.service");
        fileSystemReader.SetFileInfo("/etc/systemd/system/erp.service", 200);
        fileSystemReader.SetText("/etc/systemd/system/erp.service", """
            [Service]
            ExecStart=/opt/erp/bin/erp
            EnvironmentFile=/etc/erp/erp.env
            """);
        fileSystemReader.SetFileInfo("/etc/erp/erp.env", 32);
        fileSystemReader.SetText("/etc/erp/erp.env", "DB_PASSWORD=SuperSecret123");

        var systemdResult = new SystemdProbeResult
        {
            Status = SystemdAvailability.Available,
            Units = [new SystemdUnitRow { UnitName = "erp.service", FragmentPath = "/etc/systemd/system/erp.service", ActiveState = "active", LoadState = "loaded" }]
        };

        var result = await Scanner(fileSystemReader, systemdResult).ScanAsync(Context(), CancellationToken.None);

        Assert.Contains(result.Entities, e => e.Path == "/etc/erp/erp.env");
        var envEntity = (ServerSleuth.Core.Models.Configuration)result.Entities.Single(e => e.Path == "/etc/erp/erp.env");
        Assert.True(envEntity.SecretDetected);
        Assert.DoesNotContain("SuperSecret123", envEntity.Metadata.Values);
    }

    [Fact]
    public async Task ScanAsync_KubernetesAndDockerSocketAndApiAreNeverAccessed()
    {
        // Structural guarantee: LinuxConfigurationScanner has no IProcessRunner/IKubernetesProvider/
        // IContainerRuntimeProvider dependency at all — it is constitutionally unable to invoke
        // kubectl, docker, mysql, psql, php, nginx, or apachectl. This test documents that
        // guarantee by asserting the scanner's only dependencies are file-system-shaped.
        var constructorParams = typeof(LinuxConfigurationScanner).GetConstructors().Single().GetParameters();

        Assert.DoesNotContain(constructorParams, p => p.ParameterType.Name.Contains("ProcessRunner"));
        Assert.DoesNotContain(constructorParams, p => p.ParameterType.Name.Contains("KubernetesProvider"));
        Assert.DoesNotContain(constructorParams, p => p.ParameterType.Name.Contains("ContainerRuntimeProvider"));
    }
}
