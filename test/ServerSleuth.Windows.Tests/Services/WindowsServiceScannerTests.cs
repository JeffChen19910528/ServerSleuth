using ServerSleuth.Core.Enums;
using ServerSleuth.Windows.Services;

namespace ServerSleuth.Windows.Tests.Services;

public class WindowsServiceScannerTests
{
    private static ServiceSnapshot MakeSnapshot(string status = "Running") => new()
    {
        ServiceName = "ERPService",
        DisplayName = "ERP Background Service",
        Status = status,
        ServiceType = "Win32OwnProcess"
    };

    [Fact]
    public void BuildEntity_FullDetail_MapsExecutableAccountAndDependencies()
    {
        var detail = new ServiceRegistryDetail
        {
            ImagePath = @"D:\ERP\Service\ERPService.exe",
            ObjectName = @"CONTOSO\svc-erp",
            Description = "Runs ERP background jobs",
            StartMode = 2,
            DelayedAutoStart = false,
            DependOnService = ["RpcSs", "Tcpip"]
        };

        var entity = WindowsServiceScanner.BuildEntity(MakeSnapshot(), detail);

        Assert.Equal(@"D:\ERP\Service\ERPService.exe", entity.ExecutablePath);
        Assert.Equal(@"CONTOSO\svc-erp", entity.ServiceAccount);
        Assert.Equal("Automatic", entity.StartType);
        Assert.Equal(2, entity.Dependencies.Count);
        Assert.Equal(EntityStatus.Running, entity.Status);
    }

    [Fact]
    public void BuildEntity_CustomServiceAccount_IsTaggedMigrationRelevant()
    {
        var detail = new ServiceRegistryDetail { ObjectName = @"CONTOSO\svc-erp" };

        var entity = WindowsServiceScanner.BuildEntity(MakeSnapshot(), detail);

        Assert.Contains("MigrationRelevant", entity.Tags);
    }

    [Theory]
    [InlineData("LocalSystem")]
    [InlineData("NT AUTHORITY\\NetworkService")]
    [InlineData("NT AUTHORITY\\LocalService")]
    public void BuildEntity_BuiltInServiceAccount_IsNotTaggedMigrationRelevant(string account)
    {
        var detail = new ServiceRegistryDetail { ObjectName = account };

        var entity = WindowsServiceScanner.BuildEntity(MakeSnapshot(), detail);

        Assert.DoesNotContain("MigrationRelevant", entity.Tags);
    }

    [Fact]
    public void BuildEntity_DelayedAutoStart_ProducesDistinctStartTypeLabel()
    {
        var detail = new ServiceRegistryDetail { StartMode = 2, DelayedAutoStart = true };

        var entity = WindowsServiceScanner.BuildEntity(MakeSnapshot(), detail);

        Assert.Equal("Automatic (Delayed Start)", entity.StartType);
    }

    [Theory]
    [InlineData(0, "Boot")]
    [InlineData(1, "System")]
    [InlineData(3, "Manual")]
    [InlineData(4, "Disabled")]
    public void BuildEntity_StartModeMapping_MatchesWindowsSemantics(int startMode, string expected)
    {
        var detail = new ServiceRegistryDetail { StartMode = startMode };

        var entity = WindowsServiceScanner.BuildEntity(MakeSnapshot(), detail);

        Assert.Equal(expected, entity.StartType);
    }

    [Fact]
    public void BuildEntity_NoRegistryDetail_LeavesExecutablePathNullAndRecordsUnavailable()
    {
        var entity = WindowsServiceScanner.BuildEntity(MakeSnapshot(), new ServiceRegistryDetail());

        Assert.Null(entity.ExecutablePath);
        Assert.Equal("Unavailable", entity.Metadata["ExecutablePathStatus"]);
    }

    [Fact]
    public void BuildEntity_StoppedService_MapsToInstalledNotRunning()
    {
        var entity = WindowsServiceScanner.BuildEntity(MakeSnapshot(status: "Stopped"), new ServiceRegistryDetail());

        Assert.Equal(EntityStatus.Installed, entity.Status);
    }

    [Fact]
    public void BuildEntity_HasRecoveryConfiguration_RecordsMetadataFlag()
    {
        var detail = new ServiceRegistryDetail { HasRecoveryConfiguration = true };

        var entity = WindowsServiceScanner.BuildEntity(MakeSnapshot(), detail);

        Assert.Equal("true", entity.Metadata["HasRecoveryConfiguration"]);
    }
}
