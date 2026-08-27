using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Linux.Cron;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Cron;

public class LinuxScheduledTaskScannerScanAsyncTests
{
    private static DiscoveryContext Context() => new() { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None };

    private static LinuxScheduledTaskScanner Scanner(FakeFileSystemReader fs) =>
        new(fs, new SecretRedactor(), NullLogger<LinuxScheduledTaskScanner>.Instance);

    [Fact]
    public async Task ScanAsync_EtcCrontabWithMultipleJobs_DiscoversAll()
    {
        var fs = new FakeFileSystemReader();
        fs.SetText("/etc/crontab",
            "# comment\n" +
            "PATH=/usr/bin:/bin\n" +
            "17 *\t* * *\troot\tcd / && run-parts /etc/cron.hourly\n" +
            "25 6\t* * *\troot\ttest -x /usr/sbin/anacron || run-parts /etc/cron.daily\n");

        var result = await Scanner(fs).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(2, result.Entities.Count);
        Assert.Equal(ScannerStatus.Supported, result.Status);
    }

    [Fact]
    public async Task ScanAsync_CronDDirectory_DiscoversEachFile()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileEntries("/etc/cron.d", "/etc/cron.d/erp-nightly");
        fs.SetText("/etc/cron.d/erp-nightly", "0 2 * * *\troot\t/opt/erp/bin/nightly-backup.sh\n");

        var result = await Scanner(fs).ScanAsync(Context(), CancellationToken.None);

        var task = Assert.Single(result.Entities.OfType<Core.Models.ScheduledTask>());
        Assert.Equal("/opt/erp/bin/nightly-backup.sh", task.Action);
    }

    [Fact]
    public async Task ScanAsync_CronDailyRunParts_EachFileBecomesOneJobWithDailyTrigger()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileEntries("/etc/cron.daily", "/etc/cron.daily/logrotate", "/etc/cron.daily/apt-compat");

        var result = await Scanner(fs).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(2, result.Entities.Count);
        Assert.All(result.Entities.OfType<Core.Models.ScheduledTask>(), t => Assert.Equal("Daily", t.Trigger));
    }

    [Fact]
    public async Task ScanAsync_UserCrontab_DebianStyleLayout_DiscoversJob()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileEntries("/var/spool/cron/crontabs", "/var/spool/cron/crontabs/erpuser");
        fs.SetText("/var/spool/cron/crontabs/erpuser", "0 3 * * * /opt/erp/bin/worker --sync\n");

        var result = await Scanner(fs).ScanAsync(Context(), CancellationToken.None);

        var task = Assert.Single(result.Entities.OfType<Core.Models.ScheduledTask>());
        Assert.Equal("erpuser", task.RunAsAccount);
    }

    [Fact]
    public async Task ScanAsync_PermissionDeniedSource_ReturnsPartiallySupported_NeverCrashes()
    {
        var fs = new FakeFileSystemReader();
        fs.SetTextFailure("/etc/crontab", OperationStatus.AccessDenied);

        var result = await Scanner(fs).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Contains(result.Errors, e => e.IsPermissionFailure);
    }

    [Fact]
    public async Task ScanAsync_NoSourcesPresent_ReturnsSupportedWithEmptyResult()
    {
        var result = await Scanner(new FakeFileSystemReader()).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Empty(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_IdenticalJobInTwoDifferentFiles_ProducesTwoDistinctEntities_NeverMerged()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileEntries("/etc/cron.d", "/etc/cron.d/a", "/etc/cron.d/b");
        fs.SetText("/etc/cron.d/a", "0 2 * * *\troot\t/opt/erp/bin/worker\n");
        fs.SetText("/etc/cron.d/b", "0 2 * * *\troot\t/opt/erp/bin/worker\n");

        var result = await Scanner(fs).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(2, result.Entities.Count);
        Assert.Equal(2, result.Entities.Select(e => e.Id).Distinct().Count());
    }
}
