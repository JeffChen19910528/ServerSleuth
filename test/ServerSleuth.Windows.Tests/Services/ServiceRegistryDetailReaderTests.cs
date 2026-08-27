using Microsoft.Win32;
using ServerSleuth.Windows.Services;
using ServerSleuth.Windows.Tests.Fakes;

namespace ServerSleuth.Windows.Tests.Services;

public class ServiceRegistryDetailReaderTests
{
    [Fact]
    public void Read_ServiceKeyPresent_MapsAllFields()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, @"SYSTEM\CurrentControlSet\Services\ERPService",
            new Dictionary<string, object?>
            {
                ["ImagePath"] = @"D:\ERP\ERPService.exe",
                ["ObjectName"] = @"CONTOSO\svc-erp",
                ["Description"] = "ERP jobs",
                ["Start"] = 2,
                ["DelayedAutostart"] = 1,
                ["DependOnService"] = new[] { "RpcSs" },
                ["FailureActions"] = new byte[] { 1, 2, 3 }
            });
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, @"SYSTEM\CurrentControlSet\Services\ERPService\Parameters",
            new Dictionary<string, object?> { ["ServiceDll"] = @"C:\Windows\System32\erpsvc.dll" });

        var detail = ServiceRegistryDetailReader.Read(reader, "ERPService");

        Assert.Equal(@"D:\ERP\ERPService.exe", detail.ImagePath);
        Assert.Equal(@"CONTOSO\svc-erp", detail.ObjectName);
        Assert.Equal(2, detail.StartMode);
        Assert.True(detail.DelayedAutoStart);
        Assert.Single(detail.DependOnService);
        Assert.Equal(@"C:\Windows\System32\erpsvc.dll", detail.ServiceDll);
        Assert.True(detail.HasRecoveryConfiguration);
    }

    [Fact]
    public void Read_ServiceKeyMissing_ReturnsEmptyDetailWithoutThrowing()
    {
        var reader = new FakeWindowsRegistryReader();

        var detail = ServiceRegistryDetailReader.Read(reader, "DoesNotExist");

        Assert.Null(detail.ImagePath);
        Assert.Empty(detail.DependOnService);
        Assert.False(detail.HasRecoveryConfiguration);
    }

    [Fact]
    public void Read_AccessDeniedOnServiceKey_ReturnsEmptyDetailWithoutThrowing()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetAccessDenied(RegistryHive.LocalMachine, RegistryView.Registry64, @"SYSTEM\CurrentControlSet\Services\Protected");

        var detail = ServiceRegistryDetailReader.Read(reader, "Protected");

        Assert.Null(detail.ImagePath);
    }
}
