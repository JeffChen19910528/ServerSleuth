using ServerSleuth.Core.Models;
using ServerSleuth.Windows.Configuration;

namespace ServerSleuth.Windows.Tests.Configuration;

public class ScanRootCollectorTests
{
    private static WebSite MakeSite(string physicalPath) => new()
    {
        Id = "site-1", Name = "ERP", Type = "WebSite", Source = "Test", PhysicalPath = physicalPath
    };

    private static Application MakeApp(string path) => new()
    {
        Id = "app-1", Name = "ERP/api", Type = "Application", Source = "Test", Path = path
    };

    private static Service MakeService(string exePath) => new()
    {
        Id = "svc-1", Name = "ERPService", Type = "Service", Source = "Test", ExecutablePath = exePath
    };

    private static ScheduledTask MakeTask(string action) => new()
    {
        Id = "task-1", Name = "NightlyJob", Type = "ScheduledTask", Source = "Test", Action = action, Enabled = true
    };

    [Fact]
    public void Collect_IisSitePhysicalPath_BecomesScanRoot()
    {
        var roots = ScanRootCollector.Collect([MakeSite(@"D:\Web\ERP")], [], [], []);

        var root = Assert.Single(roots);
        Assert.Equal(@"D:\Web\ERP", root.Path);
        Assert.Equal("IIS", root.Source);
        Assert.Equal("site-1", root.OwnerEntityId);
    }

    [Fact]
    public void Collect_ServiceExecutablePath_UsesDirectoryNotFullExePath()
    {
        var roots = ScanRootCollector.Collect([], [], [MakeService(@"D:\ERP\Service\ERPService.exe")], []);

        var root = Assert.Single(roots);
        Assert.Equal(@"D:\ERP\Service", root.Path);
        Assert.Equal("WindowsService", root.Source);
    }

    [Fact]
    public void Collect_ScheduledTaskExecutableAction_UsesDirectory()
    {
        var roots = ScanRootCollector.Collect([], [], [], [MakeTask(@"D:\ERP\NightlyJob.exe")]);

        var root = Assert.Single(roots);
        Assert.Equal(@"D:\ERP", root.Path);
        Assert.Equal("ScheduledTask", root.Source);
    }

    [Fact]
    public void Collect_ScheduledTaskNonExecutableAction_IsSkipped()
    {
        // e.g. a ComHandler or ShowMessage action with no filesystem path
        var roots = ScanRootCollector.Collect([], [], [], [MakeTask("ComHandler")]);

        Assert.Empty(roots);
    }

    [Fact]
    public void Collect_SamePathFromTwoSources_IsDeduplicated()
    {
        var roots = ScanRootCollector.Collect(
            [MakeSite(@"D:\Web\ERP")],
            [MakeApp(@"D:\Web\ERP")],
            [],
            []);

        Assert.Single(roots);
    }

    [Fact]
    public void Collect_DeduplicationIsCaseInsensitiveAndTrailingSlashInsensitive()
    {
        var roots = ScanRootCollector.Collect(
            [MakeSite(@"D:\Web\ERP\")],
            [MakeApp(@"d:\web\erp")],
            [],
            []);

        Assert.Single(roots);
    }

    [Fact]
    public void Collect_EntitiesWithNoPath_ProduceNoScanRoot()
    {
        var site = new WebSite { Id = "s", Name = "s", Type = "WebSite", Source = "Test", PhysicalPath = null };

        var roots = ScanRootCollector.Collect([site], [], [], []);

        Assert.Empty(roots);
    }

    [Fact]
    public void Collect_NoEntitiesAtAll_ReturnsEmpty()
    {
        var roots = ScanRootCollector.Collect([], [], [], []);

        Assert.Empty(roots);
    }
}
