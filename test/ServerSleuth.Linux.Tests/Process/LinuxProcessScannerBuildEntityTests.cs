using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Linux.Process;

namespace ServerSleuth.Linux.Tests.Process;

public class LinuxProcessScannerBuildEntityTests
{
    [Fact]
    public void BuildEntity_NormalProcess_MapsAllFields()
    {
        var snapshot = new ProcProcessSnapshot
        {
            Pid = 1234,
            ParentPid = 1,
            Name = "nginx",
            State = "S (sleeping)",
            CommandLine = "/usr/sbin/nginx -g daemon off;",
            ExecutablePath = "/usr/sbin/nginx",
            Uid = "0"
        };

        var entity = LinuxProcessScanner.BuildEntity(snapshot);

        Assert.Equal("process:1234", entity.Id);
        Assert.Equal(1234, entity.Pid);
        Assert.Equal(1, entity.ParentPid);
        Assert.Equal("/usr/sbin/nginx", entity.Path);
        Assert.Equal("/usr/sbin/nginx -g daemon off;", entity.CommandLine);
        Assert.Equal(ConfidenceBand.VeryHigh, entity.Confidence.Band);
    }

    [Fact]
    public void BuildEntity_AccessDenied_ProducesLowConfidenceAndMetadataFlag()
    {
        var snapshot = new ProcProcessSnapshot { Pid = 42, AccessDenied = true };

        var entity = LinuxProcessScanner.BuildEntity(snapshot);

        Assert.Equal("AccessDenied", entity.Metadata["AccessStatus"]);
        Assert.NotEqual(ConfidenceBand.VeryHigh, entity.Confidence.Band);
    }

    [Fact]
    public void BuildEntity_MalformedEntry_ProducesMalformedMetadataFlag()
    {
        var snapshot = new ProcProcessSnapshot { Pid = 99, MalformedEntry = true };

        var entity = LinuxProcessScanner.BuildEntity(snapshot);

        Assert.Equal("MalformedEntry", entity.Metadata["AccessStatus"]);
    }

    [Fact]
    public void BuildEntity_ZombieOrKernelThread_NoExecutablePath_RecordsUnavailable()
    {
        var snapshot = new ProcProcessSnapshot { Pid = 7, Name = "kthreadd", State = "S", ExecutablePath = null };

        var entity = LinuxProcessScanner.BuildEntity(snapshot);

        Assert.Null(entity.Path);
        Assert.Equal("Unavailable", entity.Metadata["ExecutablePathStatus"]);
        Assert.Equal(ConfidenceBand.VeryHigh, entity.Confidence.Band); // still a legitimately-read, non-malformed entry
    }

    [Fact]
    public void BuildEntity_NoName_UsesPidFallbackName()
    {
        var snapshot = new ProcProcessSnapshot { Pid = 55, AccessDenied = true };

        var entity = LinuxProcessScanner.BuildEntity(snapshot);

        Assert.Equal("pid-55", entity.Name);
    }
}
