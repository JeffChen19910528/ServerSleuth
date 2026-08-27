using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Linux.OperatingSystem;
using CoreOperatingSystem = ServerSleuth.Core.Models.OperatingSystem;

namespace ServerSleuth.Linux.Tests.OperatingSystem;

public class LinuxOsScannerBuildEntitiesTests
{
    [Fact]
    public void BuildEntities_FullSnapshot_ProducesServerAndOsWithHighConfidence()
    {
        var snapshot = new LinuxOsSnapshot
        {
            OsReleaseAvailable = true,
            OsRelease = new Dictionary<string, string> { ["PRETTY_NAME"] = "Ubuntu 22.04.3 LTS", ["ID"] = "ubuntu", ["VERSION_ID"] = "22.04" },
            Hostname = "erp-web-01",
            KernelRelease = "5.15.0-91-generic",
            OsType = "Linux",
            UnameMachine = "x86_64"
        };

        var entities = LinuxOsScanner.BuildEntities(snapshot);

        var server = Assert.IsType<Server>(entities.Single(e => e is Server));
        Assert.Equal("erp-web-01", server.Hostname);

        var os = Assert.IsType<CoreOperatingSystem>(entities.Single(e => e is CoreOperatingSystem));
        Assert.Equal("Ubuntu 22.04.3 LTS", os.Platform);
        Assert.Equal("22.04", os.Version);
        Assert.Equal("5.15.0-91-generic", os.Kernel);
        Assert.Equal(EntityArchitecture.X64, os.Architecture);
        Assert.Equal(ConfidenceBand.VeryHigh, os.Confidence.Band);
    }

    [Fact]
    public void BuildEntities_OsReleaseMissing_FallsBackToProcSysKernel_WithLowerConfidence()
    {
        var snapshot = new LinuxOsSnapshot
        {
            OsReleaseAvailable = false,
            OsRelease = new Dictionary<string, string>(),
            Hostname = "host1",
            KernelRelease = "6.2.0-generic",
            OsType = "Linux",
            UnameMachine = "aarch64"
        };

        var entities = LinuxOsScanner.BuildEntities(snapshot);

        var os = entities.OfType<CoreOperatingSystem>().Single();
        Assert.Equal("Linux", os.Name);
        Assert.Equal("6.2.0-generic", os.Kernel);
        Assert.Equal(EntityArchitecture.Arm64, os.Architecture);
        Assert.NotEqual(ConfidenceBand.VeryHigh, os.Confidence.Band);
    }

    [Fact]
    public void BuildEntities_NoHostnameAvailable_UsesPrettyNameFallbackForId()
    {
        var snapshot = new LinuxOsSnapshot
        {
            OsReleaseAvailable = true,
            OsRelease = new Dictionary<string, string> { ["PRETTY_NAME"] = "Debian 12" },
            Hostname = null
        };

        var entities = LinuxOsScanner.BuildEntities(snapshot);

        var server = entities.OfType<Server>().Single();
        Assert.Equal("server:Debian 12", server.Id);
        Assert.Null(server.Hostname);
    }

    [Fact]
    public void BuildEntities_NoUnameMachine_ArchitectureUnknown_MetadataRecordsUnavailable()
    {
        var snapshot = new LinuxOsSnapshot { OsReleaseAvailable = true, OsRelease = new Dictionary<string, string> { ["PRETTY_NAME"] = "Ubuntu" }, Hostname = "h1" };

        var os = LinuxOsScanner.BuildEntities(snapshot).OfType<CoreOperatingSystem>().Single();

        Assert.Equal(EntityArchitecture.Unknown, os.Architecture);
        Assert.Equal("Unavailable", os.Metadata["ArchitectureStatus"]);
    }
}
