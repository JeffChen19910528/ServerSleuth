using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.ViewModels.Results;

/// <summary>
/// GUI-10 §8 — proves <see cref="InventoryDetailViewModel"/> surfaces
/// <see cref="ScheduledTask"/>'s own typed fields (Folder, Trigger, Action, RunAsAccount,
/// Enabled, NextRun), which were previously invisible in this panel (only base
/// <see cref="DiscoveryEntity"/> fields and <c>Metadata</c> rendered, and neither
/// WindowsScheduledTaskScanner nor LinuxScheduledTaskScanner puts these specific fields into
/// <c>Metadata</c> — they are typed properties). No scanner is touched or invoked here; this is
/// presentation over an already-built, hand-crafted <see cref="ScheduledTask"/> fixture, exactly
/// like every other GUI-6A Inventory test.
/// </summary>
public class InventoryScheduledTaskDetailTests
{
    private static ScheduledTask BuildTask(DateTimeOffset? nextRun = null) => new()
    {
        Id = "scheduledtask:\\ERP\\Nightly",
        Name = "Nightly",
        Type = "ScheduledTask",
        Source = "WindowsTaskScheduler",
        Status = EntityStatus.Configured,
        Confidence = Confidence.VeryHigh(),
        Folder = @"\ERP",
        Trigger = "Daily",
        Action = @"C:\ERP\Worker\ERPWorker.exe",
        RunAsAccount = "NT AUTHORITY\\SYSTEM",
        Enabled = true,
        NextRun = nextRun
    };

    [Fact]
    public void ScheduledTaskFields_AreSurfacedOnTheDetailViewModel()
    {
        var nextRun = new DateTimeOffset(2026, 9, 4, 2, 0, 0, TimeSpan.Zero);
        var task = BuildTask(nextRun);
        var options = new ScanResultFixtureFactory.Options { ApplicationCount = 0, DiscoveryEntities = [task] };
        var state = ScanResultFixtureFactory.BuildCompletedState(options);
        var vm = new ResultsDashboardViewModel(state);

        var item = Assert.Single(vm.Inventory.Items);
        var detail = item.Detail;

        Assert.True(detail.IsScheduledTask);
        Assert.Equal(@"\ERP", detail.ScheduledTaskFolder);
        Assert.Equal("Daily", detail.ScheduledTaskTrigger);
        Assert.Equal(@"C:\ERP\Worker\ERPWorker.exe", detail.ScheduledTaskAction);
        Assert.Equal("NT AUTHORITY\\SYSTEM", detail.ScheduledTaskRunAsAccount);
        Assert.Equal(true, detail.ScheduledTaskEnabled);
        Assert.Equal(nextRun, detail.ScheduledTaskNextRun);
    }

    [Fact]
    public void NonScheduledTaskEntity_HasNullScheduledTaskFields_NeverFabricated()
    {
        var service = new Service
        {
            Id = "service:Worker",
            Name = "Worker",
            Type = "Service",
            Source = "ServiceControlManager",
            Status = EntityStatus.Running,
            Confidence = Confidence.VeryHigh()
        };
        var options = new ScanResultFixtureFactory.Options { ApplicationCount = 0, DiscoveryEntities = [service] };
        var state = ScanResultFixtureFactory.BuildCompletedState(options);
        var vm = new ResultsDashboardViewModel(state);

        var item = Assert.Single(vm.Inventory.Items);
        var detail = item.Detail;

        Assert.False(detail.IsScheduledTask);
        Assert.Null(detail.ScheduledTaskFolder);
        Assert.Null(detail.ScheduledTaskTrigger);
        Assert.Null(detail.ScheduledTaskAction);
        Assert.Null(detail.ScheduledTaskRunAsAccount);
        Assert.Null(detail.ScheduledTaskEnabled);
        Assert.Null(detail.ScheduledTaskNextRun);
    }

    [Fact]
    public void LinuxCronScheduledTask_UsesTheSameFieldsAsWindows_NoSpecialCasing()
    {
        // LinuxScheduledTaskScanner produces the same Core.Models.ScheduledTask type — the GUI
        // must display it identically, with no OS-specific branch (skill.md GUI-10 §8).
        var cronTask = new ScheduledTask
        {
            Id = "scheduledtask:cron:/etc/cron.d/backup:0",
            Name = "/etc/cron.d/backup#0",
            Type = "ScheduledTask",
            Source = "Cron",
            Status = EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Folder = "/etc/cron.d/backup",
            Trigger = "0 2 * * *",
            Action = "/usr/local/bin/backup.sh",
            RunAsAccount = "root",
            Enabled = true
        };
        var options = new ScanResultFixtureFactory.Options { ApplicationCount = 0, DiscoveryEntities = [cronTask] };
        var state = ScanResultFixtureFactory.BuildCompletedState(options);
        var vm = new ResultsDashboardViewModel(state);

        var detail = Assert.Single(vm.Inventory.Items).Detail;

        Assert.True(detail.IsScheduledTask);
        Assert.Equal("0 2 * * *", detail.ScheduledTaskTrigger);
        Assert.Equal("/usr/local/bin/backup.sh", detail.ScheduledTaskAction);
        Assert.Equal("root", detail.ScheduledTaskRunAsAccount);
    }
}
