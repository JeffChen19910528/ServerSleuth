using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Linux.Cron;

namespace ServerSleuth.Linux.Tests.Cron;

public class LinuxScheduledTaskScannerBuildEntityTests
{
    private static readonly ISecretRedactor Redactor = new SecretRedactor();

    [Fact]
    public void BuildEntity_ExplicitExecutablePath_IsRecordedAsAction()
    {
        var entry = new CronEntry { Schedule = "0 2 * * *", User = "root", Command = "/opt/erp/bin/nightly-backup.sh --full" };

        var entity = LinuxScheduledTaskScanner.BuildEntity("/etc/crontab", 0, entry, Redactor);

        Assert.Equal("/opt/erp/bin/nightly-backup.sh", entity.Action);
        Assert.Equal("root", entity.RunAsAccount);
        Assert.Equal("0 2 * * *", entity.Trigger);
        Assert.True(entity.Enabled);
    }

    [Fact]
    public void BuildEntity_UnresolvedCommand_RecordsUnresolvedStatus()
    {
        var entry = new CronEntry { Schedule = "0 2 * * *", Command = "python3 /opt/erp/script.py" };

        var entity = LinuxScheduledTaskScanner.BuildEntity("/var/spool/cron/crontabs/erpuser", 0, entry, Redactor);

        Assert.Equal("Unresolved", entity.Metadata["ExecutablePathStatus"]);
    }

    [Fact]
    public void BuildEntity_CommandWithSecret_IsRedactedInMetadata_NeverStoresRawSecret()
    {
        var entry = new CronEntry { Schedule = "0 2 * * *", Command = "/opt/erp/bin/sync.sh --password=SuperSecret123" };

        var entity = LinuxScheduledTaskScanner.BuildEntity("/etc/crontab", 0, entry, Redactor);

        Assert.DoesNotContain("SuperSecret123", entity.Metadata["Command"]);
        Assert.Equal("true", entity.Metadata["SecretDetected"]);
    }

    [Fact]
    public void BuildEntity_DifferentSourceFiles_ProduceDistinctIdsEvenForIdenticalCommand()
    {
        var entry = new CronEntry { Schedule = "0 2 * * *", Command = "/opt/erp/bin/worker" };

        var entityA = LinuxScheduledTaskScanner.BuildEntity("/etc/cron.d/erp-a", 0, entry, Redactor);
        var entityB = LinuxScheduledTaskScanner.BuildEntity("/etc/cron.d/erp-b", 0, entry, Redactor);

        Assert.NotEqual(entityA.Id, entityB.Id);
    }
}
