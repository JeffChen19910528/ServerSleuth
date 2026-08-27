using ServerSleuth.Core.Models;
using ServerSleuth.Linux.Configuration;

namespace ServerSleuth.Linux.Tests.Configuration;

public class LinuxScanRootCollectorTests
{
    private static Service MakeService(string execPath) => new()
    {
        Id = "service:erp", Name = "erp", Type = "Service", Source = "Test", ExecutablePath = execPath
    };

    private static ScheduledTask MakeTask(string action) => new()
    {
        Id = "scheduledtask:cron:erp", Name = "erp-job", Type = "ScheduledTask", Source = "Test", Action = action, Enabled = true
    };

    [Fact]
    public void Collect_AlwaysIncludesWellKnownTechnologyRoots()
    {
        var roots = LinuxScanRootCollector.Collect([], []);

        Assert.Contains(roots, r => r.Path == "/etc/nginx" && r.Source == "Nginx");
        Assert.Contains(roots, r => r.Path == "/etc/ssh" && r.Source == "Ssh");
        Assert.Contains(roots, r => r.Path == "/etc/mysql" && r.Source == "MySql");
        Assert.Contains(roots, r => r.Path == "/etc/postgresql" && r.Source == "PostgreSql");
        Assert.Contains(roots, r => r.Path == "/etc/docker" && r.Source == "Docker");
        Assert.Contains(roots, r => r.Path == "/etc/systemd/system" && r.Source == "Systemd");
    }

    [Fact]
    public void Collect_NeverIncludesEtcCronD_DeliberatelyExcludedToAvoidDuplicatingPhase6B()
    {
        var roots = LinuxScanRootCollector.Collect([], []);

        Assert.DoesNotContain(roots, r => r.Path == "/etc/cron.d");
    }

    [Fact]
    public void Collect_ServiceExecutableInBinDirectory_ClimbsOneLevelToApplicationRoot()
    {
        var roots = LinuxScanRootCollector.Collect([MakeService("/opt/erp/bin/erp")], []);

        Assert.Contains(roots, r => r.Path == "/opt/erp" && r.Source == "ApplicationRoot" && r.OwnerEntityId == "service:erp");
    }

    [Fact]
    public void Collect_ServiceExecutableInSbinDirectory_ClimbsOneLevel()
    {
        var roots = LinuxScanRootCollector.Collect([MakeService("/opt/erp/sbin/erpd")], []);

        Assert.Contains(roots, r => r.Path == "/opt/erp");
    }

    [Fact]
    public void Collect_ServiceExecutableNotInBinOrSbin_UsesDirectoryAsIs_NeverClimbs()
    {
        var roots = LinuxScanRootCollector.Collect([MakeService("/usr/local/myapp/myapp")], []);

        Assert.Contains(roots, r => r.Path == "/usr/local/myapp");
    }

    [Fact]
    public void Collect_ExecutableDirectlyUnderBin_NeverClimbsToFilesystemRoot()
    {
        var roots = LinuxScanRootCollector.Collect([MakeService("/bin/erp")], []);

        Assert.DoesNotContain(roots, r => r.Path == "/");
        Assert.Contains(roots, r => r.Path == "/bin");
    }

    [Fact]
    public void Collect_ScheduledTaskWithRootedAction_DerivesApplicationRoot()
    {
        var roots = LinuxScanRootCollector.Collect([], [MakeTask("/opt/erp/bin/worker")]);

        Assert.Contains(roots, r => r.Path == "/opt/erp" && r.OwnerEntityId == "scheduledtask:cron:erp");
    }

    [Fact]
    public void Collect_ScheduledTaskWithUnresolvedNonRootedAction_ProducesNoRoot()
    {
        var roots = LinuxScanRootCollector.Collect([], [MakeTask("python3 script.py")]);

        Assert.DoesNotContain(roots, r => r.OwnerEntityId == "scheduledtask:cron:erp");
    }

    [Fact]
    public void Collect_DuplicatePathsFromDifferentOwners_AreDeduplicated()
    {
        var roots = LinuxScanRootCollector.Collect(
            [MakeService("/opt/erp/bin/erp")],
            [MakeTask("/opt/erp/bin/backup")]);

        Assert.Single(roots, r => r.Path == "/opt/erp");
    }

    [Fact]
    public void Collect_PathsAreCaseSensitive_NeverLowercased()
    {
        var roots = LinuxScanRootCollector.Collect([MakeService("/opt/ERP/bin/erp")], []);

        Assert.Contains(roots, r => r.Path == "/opt/ERP");
        Assert.DoesNotContain(roots, r => r.Path == "/opt/erp");
    }

    [Fact]
    public void DeriveApplicationRoot_NullExecutablePath_ReturnsNull()
    {
        Assert.Null(LinuxScanRootCollector.DeriveApplicationRoot(null));
    }
}
