using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Linux.Cron;
using ServerSleuth.Linux.Native;
using ServerSleuth.Linux.Process;
using ServerSleuth.Linux.Runtimes;
using ServerSleuth.Linux.Systemd;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Native;

public class LinuxNativeDependencyScannerScanAsyncTests
{
    private static DiscoveryContext Context() => new() { Profile = ScanProfile.Migration, CancellationToken = CancellationToken.None };

    private sealed class FakeSystemdProvider(SystemdProbeResult result) : ISystemdProvider
    {
        public SystemdProbeResult GetSnapshot() => result;
    }

    private sealed class FakeLdconfigProvider(IReadOnlyDictionary<string, string>? cache = null) : ILdconfigProvider
    {
        public Task<IReadOnlyDictionary<string, string>> GetCacheAsync(CancellationToken cancellationToken) =>
            Task.FromResult(cache ?? new Dictionary<string, string>());
    }

    private static LinuxNativeDependencyScanner Scanner(
        FakeFileSystemReader fileSystemReader,
        IReadOnlyList<ProcProcessSnapshot>? processes = null,
        SystemdProbeResult? systemdResult = null,
        ILdconfigProvider? ldconfigProvider = null)
    {
        var processScanner = new LinuxProcessScanner(new FakeProcProvider(processes ?? []), NullLogger<LinuxProcessScanner>.Instance);
        var systemdScanner = new LinuxSystemdServiceScanner(
            new FakeSystemdProvider(systemdResult ?? new SystemdProbeResult { Status = SystemdAvailability.Available }),
            NullLogger<LinuxSystemdServiceScanner>.Instance);
        var cronScanner = new LinuxScheduledTaskScanner(fileSystemReader, new ServerSleuth.Infrastructure.Security.SecretRedactor(), NullLogger<LinuxScheduledTaskScanner>.Instance);
        var runtimeScanner = new LinuxRuntimeDiscoveryScanner([], NullLogger<LinuxRuntimeDiscoveryScanner>.Instance);

        return new LinuxNativeDependencyScanner(
            processScanner, systemdScanner, cronScanner, runtimeScanner,
            fileSystemReader, new ElfParser(), new LinuxLibraryResolver(fileSystemReader),
            ldconfigProvider ?? new FakeLdconfigProvider(), NullLogger<LinuxNativeDependencyScanner>.Instance);
    }

    [Fact]
    public async Task ScanAsync_NoDiscoveredExecutablePaths_ReturnsSupported_WithNoEntities()
    {
        var fs = new FakeFileSystemReader();

        var result = await Scanner(fs).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Empty(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_ProcessWithExecutablePath_AnalyzesElfAndAssociatesOwner()
    {
        var fs = new FakeFileSystemReader();
        var bytes = SyntheticElfBuilder.BuildElf64(needed: ["libc.so.6"]);
        fs.SetFileInfo("/opt/erp/bin/erp", bytes.Length);
        fs.SetBytes("/opt/erp/bin/erp", bytes);

        var processes = new List<ProcProcessSnapshot> { new() { Pid = 100, Name = "erp", ExecutablePath = "/opt/erp/bin/erp" } };

        var result = await Scanner(fs, processes: processes).ScanAsync(Context(), CancellationToken.None);

        var entity = Assert.Single(result.Entities);
        Assert.Equal("/opt/erp/bin/erp", entity.Path);
        Assert.Contains(((ServerSleuth.Core.Models.Dll)entity).ReferencedByEntityIds, id => id.Contains("100"));
    }

    [Fact]
    public async Task ScanAsync_SamePathReferencedByTwoEntities_AnalyzedOnce_BothOwnersRecorded()
    {
        var fs = new FakeFileSystemReader();
        var bytes = SyntheticElfBuilder.BuildElf64();
        fs.SetFileInfo("/opt/erp/bin/erp", bytes.Length);
        fs.SetBytes("/opt/erp/bin/erp", bytes);

        var processes = new List<ProcProcessSnapshot> { new() { Pid = 100, Name = "erp", ExecutablePath = "/opt/erp/bin/erp" } };
        var systemdResult = new SystemdProbeResult
        {
            Status = SystemdAvailability.Available,
            Units = [new SystemdUnitRow { UnitName = "erp.service", ExecStart = "path=/opt/erp/bin/erp", ActiveState = "active", LoadState = "loaded" }]
        };

        var result = await Scanner(fs, processes: processes, systemdResult: systemdResult).ScanAsync(Context(), CancellationToken.None);

        var entity = (ServerSleuth.Core.Models.Dll)Assert.Single(result.Entities);
        Assert.Equal(2, entity.ReferencedByEntityIds.Count);
    }

    [Fact]
    public async Task ScanAsync_DtNeededResolvedAgainstAnotherDiscoveredBinary_UsesKnownBinaryTier()
    {
        var fs = new FakeFileSystemReader();
        var appBytes = SyntheticElfBuilder.BuildElf64(needed: ["libshared.so"]);
        var libBytes = SyntheticElfBuilder.BuildElf64();
        fs.SetFileInfo("/opt/erp/bin/erp", appBytes.Length);
        fs.SetBytes("/opt/erp/bin/erp", appBytes);
        fs.SetFileInfo("/opt/erp/lib/libshared.so", libBytes.Length);
        fs.SetBytes("/opt/erp/lib/libshared.so", libBytes);

        var processes = new List<ProcProcessSnapshot>
        {
            new() { Pid = 100, Name = "erp", ExecutablePath = "/opt/erp/bin/erp" },
            new() { Pid = 101, Name = "libshared", ExecutablePath = "/opt/erp/lib/libshared.so" }
        };

        var result = await Scanner(fs, processes: processes).ScanAsync(Context(), CancellationToken.None);

        var appEntity = result.Entities.Single(e => e.Path == "/opt/erp/bin/erp");
        Assert.Equal("Resolved", appEntity.Metadata["Dependency0.Status"]);
        Assert.Equal("KnownBinary", appEntity.Metadata["Dependency0.Source"]);
    }

    [Fact]
    public async Task ScanAsync_UnresolvableDependency_NeverFabricatesPath_ScanStillSucceeds()
    {
        var fs = new FakeFileSystemReader();
        var bytes = SyntheticElfBuilder.BuildElf64(needed: ["libvendor.so"]);
        fs.SetFileInfo("/opt/erp/bin/erp", bytes.Length);
        fs.SetBytes("/opt/erp/bin/erp", bytes);

        var processes = new List<ProcProcessSnapshot> { new() { Pid = 100, Name = "erp", ExecutablePath = "/opt/erp/bin/erp" } };

        var result = await Scanner(fs, processes: processes).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        var entity = Assert.Single(result.Entities);
        Assert.Equal("NotFound", entity.Metadata["Dependency0.Status"]);
        Assert.False(entity.Metadata.ContainsKey("Dependency0.ResolvedPath"));
    }

    [Fact]
    public async Task ScanAsync_AccessDeniedBinary_DegradesToPartiallySupported_NeverFailsWholeScan()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileInfoFailure("/opt/erp/bin/erp", OperationStatus.AccessDenied);

        var processes = new List<ProcProcessSnapshot> { new() { Pid = 100, Name = "erp", ExecutablePath = "/opt/erp/bin/erp" } };

        var result = await Scanner(fs, processes: processes).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Single(result.Errors);
        var entity = Assert.Single(result.Entities);
        Assert.Equal("AccessDenied", entity.Metadata["FileStatus"]);
    }

    [Fact]
    public async Task ScanAsync_MissingBinaryPath_RecordedAsNotFound_NeverFailsWholeScan()
    {
        var fs = new FakeFileSystemReader();

        var processes = new List<ProcProcessSnapshot> { new() { Pid = 100, Name = "erp", ExecutablePath = "/opt/erp/bin/gone" } };

        var result = await Scanner(fs, processes: processes).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        var entity = Assert.Single(result.Entities);
        Assert.Equal("NotFound", entity.Metadata["FileStatus"]);
    }

    [Fact]
    public async Task ScanAsync_LdconfigAvailable_UsedAsLastResortTier()
    {
        var fs = new FakeFileSystemReader();
        var bytes = SyntheticElfBuilder.BuildElf64(needed: ["libssl.so.3"]);
        fs.SetFileInfo("/opt/erp/bin/erp", bytes.Length);
        fs.SetBytes("/opt/erp/bin/erp", bytes);

        var processes = new List<ProcProcessSnapshot> { new() { Pid = 100, Name = "erp", ExecutablePath = "/opt/erp/bin/erp" } };
        var ldconfig = new FakeLdconfigProvider(new Dictionary<string, string> { ["libssl.so.3"] = "/usr/lib/x86_64-linux-gnu/libssl.so.3" });

        var result = await Scanner(fs, processes: processes, ldconfigProvider: ldconfig).ScanAsync(Context(), CancellationToken.None);

        var entity = Assert.Single(result.Entities);
        Assert.Equal("Ldconfig", entity.Metadata["Dependency0.Source"]);
    }

    [Fact]
    public async Task ScanAsync_LdconfigUnavailable_ScanStillSupported_NeverFailed()
    {
        var fs = new FakeFileSystemReader();
        var bytes = SyntheticElfBuilder.BuildElf64(needed: ["libc.so.6"]);
        fs.SetFileInfo("/opt/erp/bin/erp", bytes.Length);
        fs.SetBytes("/opt/erp/bin/erp", bytes);

        var processes = new List<ProcProcessSnapshot> { new() { Pid = 100, Name = "erp", ExecutablePath = "/opt/erp/bin/erp" } };

        var result = await Scanner(fs, processes: processes, ldconfigProvider: new FakeLdconfigProvider()).ScanAsync(Context(), CancellationToken.None);

        Assert.NotEqual(ScannerStatus.Failed, result.Status);
    }

    [Fact]
    public async Task ScanAsync_DuplicatePathFromSameOwnerListedTwice_StillAnalyzedOnce()
    {
        var fs = new FakeFileSystemReader();
        var bytes = SyntheticElfBuilder.BuildElf64();
        fs.SetFileInfo("/opt/erp/bin/erp", bytes.Length);
        fs.SetBytes("/opt/erp/bin/erp", bytes);

        var processes = new List<ProcProcessSnapshot>
        {
            new() { Pid = 100, Name = "erp", ExecutablePath = "/opt/erp/bin/erp" },
            new() { Pid = 101, Name = "erp", ExecutablePath = "/opt/erp/bin/erp" }
        };

        var result = await Scanner(fs, processes: processes).ScanAsync(Context(), CancellationToken.None);

        Assert.Single(result.Entities);
    }
}
