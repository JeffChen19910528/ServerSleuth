using ServerSleuth.Core.Enums;
using ServerSleuth.Windows.Process;

namespace ServerSleuth.Windows.Tests.Process;

public class WindowsProcessScannerTests
{
    [Fact]
    public void BuildEntity_FullWmiInfo_PopulatesAllFields()
    {
        var snapshot = new ProcessSnapshot { Pid = 4532, Name = "ERPService", StartTime = DateTimeOffset.UtcNow };
        var wmiInfo = new ProcessWmiInfo
        {
            ProcessId = 4532,
            ExecutablePath = @"D:\ERP\Service.exe",
            CommandLine = @"D:\ERP\Service.exe --start",
            ParentProcessId = 1000,
            OwnerDomain = "CONTOSO",
            OwnerUser = "svc-erp"
        };

        var entity = WindowsProcessScanner.BuildEntity(snapshot, wmiInfo);

        Assert.Equal(4532, entity.Pid);
        Assert.Equal(@"D:\ERP\Service.exe", entity.Path);
        Assert.Equal(@"D:\ERP\Service.exe --start", entity.CommandLine);
        Assert.Equal(1000, entity.ParentPid);
        Assert.Equal(@"CONTOSO\svc-erp", entity.User);
        Assert.Equal(EntityStatus.Running, entity.Status);
        Assert.Empty(entity.Metadata);
    }

    [Fact]
    public void BuildEntity_NoWmiInfo_LeavesCommandLineAndPathNullNotEmptyString()
    {
        var snapshot = new ProcessSnapshot { Pid = 4, Name = "System" };

        var entity = WindowsProcessScanner.BuildEntity(snapshot, wmiInfo: null);

        Assert.Null(entity.Path);
        Assert.Null(entity.CommandLine);
        Assert.Equal("Unavailable", entity.Metadata["ExecutablePathStatus"]);
        Assert.Equal("Unavailable", entity.Metadata["CommandLineStatus"]);
    }

    [Fact]
    public void BuildEntity_WmiInfoPresentButExecutablePathDenied_RecordsAccessDeniedNotUnavailable()
    {
        var snapshot = new ProcessSnapshot { Pid = 8, Name = "svchost" };
        var wmiInfo = new ProcessWmiInfo { ProcessId = 8, ExecutablePath = null, CommandLine = null };

        var entity = WindowsProcessScanner.BuildEntity(snapshot, wmiInfo);

        Assert.Equal("AccessDenied", entity.Metadata["ExecutablePathStatus"]);
    }

    [Fact]
    public void BuildEntity_StartTimeAccessDenied_RecordsMetadataInsteadOfThrowing()
    {
        var snapshot = new ProcessSnapshot { Pid = 12, Name = "csrss", StartTimeAccessDenied = true };

        var entity = WindowsProcessScanner.BuildEntity(snapshot, wmiInfo: null);

        Assert.Null(entity.StartTime);
        Assert.Equal("AccessDenied", entity.Metadata["StartTimeStatus"]);
    }

    [Fact]
    public void BuildEntity_AlwaysAddsProcessEvidence()
    {
        var snapshot = new ProcessSnapshot { Pid = 100, Name = "notepad" };

        var entity = WindowsProcessScanner.BuildEntity(snapshot, wmiInfo: null);

        Assert.Contains(entity.Evidence, e => e.Type == EvidenceType.Process && e.Location.Contains("100"));
    }
}
