using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Linux.Systemd;

namespace ServerSleuth.Linux.Tests.Systemd;

public class LinuxSystemdServiceScannerBuildEntityTests
{
    [Fact]
    public void BuildEntity_ActiveServiceWithExecStart_MapsExecutablePathAndRunningStatus()
    {
        var row = new SystemdUnitRow
        {
            UnitName = "nginx.service",
            Description = "Nginx web server",
            LoadState = "loaded",
            ActiveState = "active",
            SubState = "running",
            UnitFileState = "enabled",
            ExecStart = "{ path=/usr/sbin/nginx ; argv[]=/usr/sbin/nginx -g daemon off; }",
            User = "www-data",
            FragmentPath = "/lib/systemd/system/nginx.service"
        };

        var entity = LinuxSystemdServiceScanner.BuildEntity(row);

        Assert.Equal("service:nginx.service", entity.Id);
        Assert.Equal("/usr/sbin/nginx", entity.ExecutablePath);
        Assert.Equal("www-data", entity.ServiceAccount);
        Assert.Equal(EntityStatus.Running, entity.Status);
        Assert.Equal("enabled", entity.StartType);
        Assert.Equal(ConfidenceBand.VeryHigh, entity.Confidence.Band);
    }

    [Fact]
    public void BuildEntity_DisabledInactiveService_MapsConfiguredStatus()
    {
        var row = new SystemdUnitRow
        {
            UnitName = "backup.service",
            LoadState = "loaded",
            ActiveState = "inactive",
            SubState = "dead",
            UnitFileState = "disabled"
        };

        var entity = LinuxSystemdServiceScanner.BuildEntity(row);

        Assert.Equal(EntityStatus.Configured, entity.Status);
        Assert.Equal("disabled", entity.StartType);
    }

    [Fact]
    public void BuildEntity_NotFoundLoadState_MapsUnknownStatus()
    {
        var row = new SystemdUnitRow { UnitName = "ghost.service", LoadState = "not-found", ActiveState = "inactive" };

        var entity = LinuxSystemdServiceScanner.BuildEntity(row);

        Assert.Equal(EntityStatus.Unknown, entity.Status);
    }

    [Fact]
    public void BuildEntity_DetailUnavailable_ProducesMediumConfidenceAndMetadataFlag()
    {
        var row = new SystemdUnitRow { UnitName = "restricted.service", DetailUnavailable = true };

        var entity = LinuxSystemdServiceScanner.BuildEntity(row);

        Assert.Equal("Unavailable", entity.Metadata["DetailStatus"]);
        Assert.NotEqual(ConfidenceBand.VeryHigh, entity.Confidence.Band);
    }

    [Fact]
    public void BuildEntity_UnrecognizedExecStartShape_RecordsStatusMetadata_NeverGuessesPath()
    {
        var row = new SystemdUnitRow { UnitName = "odd.service", ExecStart = "some free-form unexpected text" };

        var entity = LinuxSystemdServiceScanner.BuildEntity(row);

        Assert.Null(entity.ExecutablePath);
        Assert.Equal("Unrecognized ExecStart shape", entity.Metadata["ExecutablePathStatus"]);
    }

    [Fact]
    public void BuildEntity_MultipleServices_EachGetsDistinctEntity()
    {
        var rowA = new SystemdUnitRow { UnitName = "a.service", ActiveState = "active", LoadState = "loaded" };
        var rowB = new SystemdUnitRow { UnitName = "b.service", ActiveState = "inactive", LoadState = "loaded" };

        var entityA = LinuxSystemdServiceScanner.BuildEntity(rowA);
        var entityB = LinuxSystemdServiceScanner.BuildEntity(rowB);

        Assert.NotEqual(entityA.Id, entityB.Id);
        Assert.Equal(EntityStatus.Running, entityA.Status);
        Assert.Equal(EntityStatus.Configured, entityB.Status);
    }
}
