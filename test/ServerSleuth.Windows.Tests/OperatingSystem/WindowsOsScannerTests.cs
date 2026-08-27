using System.Runtime.InteropServices;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Windows.OperatingSystem;
using CoreOperatingSystem = ServerSleuth.Core.Models.OperatingSystem;

namespace ServerSleuth.Windows.Tests.OperatingSystem;

public class WindowsOsScannerTests
{
    private static EnvironmentSnapshot MakeSnapshot() => new()
    {
        MachineName = "TEST-SERVER-01",
        OsDescription = "Microsoft Windows 10.0.20348",
        OsArchitecture = Architecture.X64,
        FrameworkDescription = ".NET 8.0.1",
        SystemDirectory = @"C:\Windows\system32",
        UserName = "svc-account",
        UserDomainName = "CONTOSO"
    };

    [Fact]
    public void BuildEntities_WithRegistryData_PrefersRegistryProductNameAndVeryHighConfidence()
    {
        var registryValues = new Dictionary<string, object?>
        {
            ["ProductName"] = "Windows Server 2022 Standard",
            ["EditionID"] = "ServerStandard",
            ["CurrentBuildNumber"] = "20348",
            ["UBR"] = 1970,
            ["DisplayVersion"] = "21H2"
        };

        var entities = WindowsOsScanner.BuildEntities(MakeSnapshot(), registryValues);

        var os = Assert.Single(entities.OfType<CoreOperatingSystem>());
        Assert.Equal("Windows Server 2022 Standard", os.Name);
        Assert.Equal("21H2", os.Version);
        Assert.Equal("ServerStandard", os.Edition);
        Assert.Equal(EntityArchitecture.X64, os.Architecture);
        Assert.Equal(ConfidenceBand.VeryHigh, os.Confidence.Band);
        Assert.Equal("20348.1970", os.Metadata["BuildNumber"]);
        Assert.Contains(os.Evidence, e => e.Type == EvidenceType.Registry);
    }

    [Fact]
    public void BuildEntities_WithoutRegistryData_FallsBackToRuntimeDescriptionWithLowerConfidence()
    {
        var entities = WindowsOsScanner.BuildEntities(MakeSnapshot(), registryValues: null);

        var os = Assert.Single(entities.OfType<CoreOperatingSystem>());
        Assert.Equal("Microsoft Windows 10.0.20348", os.Name);
        Assert.DoesNotContain(os.Evidence, e => e.Type == EvidenceType.Registry);
        Assert.True(os.Confidence.Value < 0.90);
    }

    [Fact]
    public void BuildEntities_AlwaysProducesServerEntityWithMachineName()
    {
        var entities = WindowsOsScanner.BuildEntities(MakeSnapshot(), registryValues: null);

        var server = Assert.Single(entities.OfType<Core.Models.Server>());
        Assert.Equal("TEST-SERVER-01", server.Hostname);
        Assert.Equal(EntityStatus.Running, server.Status);
    }

    [Fact]
    public void BuildEntities_RecordsSystemDirectoryAndExecutionUserMetadata()
    {
        var entities = WindowsOsScanner.BuildEntities(MakeSnapshot(), registryValues: null);

        var os = Assert.Single(entities.OfType<CoreOperatingSystem>());
        Assert.Equal(@"C:\Windows\system32", os.Metadata["SystemDirectory"]);
        Assert.Equal(@"CONTOSO\svc-account", os.Metadata["ExecutionUser"]);
    }
}
