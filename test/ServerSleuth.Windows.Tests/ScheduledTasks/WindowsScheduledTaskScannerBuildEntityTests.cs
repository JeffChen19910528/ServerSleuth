using ServerSleuth.Core.Enums;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.ScheduledTasks;

namespace ServerSleuth.Windows.Tests.ScheduledTasks;

public class WindowsScheduledTaskScannerBuildEntityTests
{
    private static readonly ISecretRedactor Redactor = new SecretRedactor();

    private static ScheduledTaskRow MakeRow(
        string path = @"\ERP\NightlyJob",
        bool enabled = true,
        string state = "Ready",
        IReadOnlyList<ScheduledTaskActionRow>? actions = null,
        IReadOnlyList<ScheduledTaskTriggerRow>? triggers = null) => new()
    {
        Path = path,
        Name = path[(path.LastIndexOf('\\') + 1)..],
        Enabled = enabled,
        State = state,
        Author = "CONTOSO\\admin",
        Description = "Runs the nightly ERP sync",
        RunLevel = "HighestAvailable",
        UserId = "CONTOSO\\svc-erp",
        LastRunTime = new DateTimeOffset(2026, 8, 22, 2, 0, 0, TimeSpan.Zero),
        NextRunTime = new DateTimeOffset(2026, 8, 23, 2, 0, 0, TimeSpan.Zero),
        LastTaskResult = 0,
        Actions = actions ?? [new ScheduledTaskActionRow { Type = "Execute", Path = @"D:\ERP\NightlyJob.exe", Arguments = "--sync", WorkingDirectory = @"D:\ERP" }],
        Triggers = triggers ?? [new ScheduledTaskTriggerRow { Type = "Daily", Enabled = true, StartBoundary = "2026-01-01T02:00:00" }]
    };

    [Fact]
    public void BuildEntity_PreservesFullTaskPathAsFolder()
    {
        var entity = WindowsScheduledTaskScanner.BuildEntity(MakeRow(path: @"\Microsoft\Windows\UpdateOrchestrator\Schedule Scan"), Redactor);

        Assert.Equal(@"\Microsoft\Windows\UpdateOrchestrator", entity.Folder);
        Assert.Equal("Schedule Scan", entity.Name);
    }

    [Fact]
    public void BuildEntity_ExecuteAction_RecordsExecutableArgumentsAndWorkingDirectory()
    {
        var entity = WindowsScheduledTaskScanner.BuildEntity(MakeRow(), Redactor);

        Assert.Equal(@"D:\ERP\NightlyJob.exe", entity.Action);
        Assert.Equal(@"D:\ERP\NightlyJob.exe", entity.Metadata["Action0.Path"]);
        Assert.Equal("--sync", entity.Metadata["Action0.Arguments"]);
        Assert.Equal(@"D:\ERP", entity.Metadata["Action0.WorkingDirectory"]);
    }

    [Fact]
    public void BuildEntity_PowerShellAction_ExtractsScriptPath()
    {
        var actions = new[]
        {
            new ScheduledTaskActionRow { Type = "Execute", Path = "powershell.exe", Arguments = @"-File ""D:\Scripts\Nightly.ps1""" }
        };

        var entity = WindowsScheduledTaskScanner.BuildEntity(MakeRow(actions: actions), Redactor);

        Assert.Equal(@"D:\Scripts\Nightly.ps1", entity.Metadata["Action0.ScriptPath"]);
    }

    [Fact]
    public void BuildEntity_RunAsAccount_NeverRecordsCredential()
    {
        var entity = WindowsScheduledTaskScanner.BuildEntity(MakeRow(), Redactor);

        Assert.Equal(@"CONTOSO\svc-erp", entity.RunAsAccount);
        Assert.DoesNotContain(entity.Metadata.Keys, k => k.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entity.Metadata.Keys, k => k.Contains("Credential", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildEntity_DisabledTask_IsStillDiscoveredNotSkipped()
    {
        var entity = WindowsScheduledTaskScanner.BuildEntity(MakeRow(enabled: false, state: "Disabled"), Redactor);

        Assert.False(entity.Enabled);
        Assert.Equal("Disabled", entity.Metadata["State"]);
        Assert.Equal(EntityStatus.Configured, entity.Status); // disabled != removed from discovery
    }

    [Fact]
    public void BuildEntity_RunningTask_MapsStatusToRunning()
    {
        var entity = WindowsScheduledTaskScanner.BuildEntity(MakeRow(state: "Running"), Redactor);

        Assert.Equal(EntityStatus.Running, entity.Status);
    }

    [Fact]
    public void BuildEntity_Triggers_RecordsTypeEnabledAndBoundaries()
    {
        var entity = WindowsScheduledTaskScanner.BuildEntity(MakeRow(), Redactor);

        Assert.Equal("Daily", entity.Trigger);
        Assert.Equal("Daily", entity.Metadata["Trigger0.Type"]);
        Assert.Equal("True", entity.Metadata["Trigger0.Enabled"]);
        Assert.Equal("2026-01-01T02:00:00", entity.Metadata["Trigger0.StartBoundary"]);
    }

    [Fact]
    public void BuildEntity_LastTaskResult_IsFormattedAsHex()
    {
        var row = MakeRow() with { LastTaskResult = 1 };

        var entity = WindowsScheduledTaskScanner.BuildEntity(row, Redactor);

        Assert.Equal("0x00000001", entity.Metadata["LastTaskResult"]);
    }

    [Fact]
    public void BuildEntity_NoActionsOrTriggers_DoesNotThrow()
    {
        var entity = WindowsScheduledTaskScanner.BuildEntity(MakeRow(actions: [], triggers: []), Redactor);

        Assert.Null(entity.Action);
        Assert.Null(entity.Trigger);
        Assert.Equal("0", entity.Metadata["ActionCount"]);
        Assert.Equal("0", entity.Metadata["TriggerCount"]);
    }

    [Fact]
    public void BuildEntity_DescriptionContainingSecret_IsRedactedAndFlagged()
    {
        var row = MakeRow() with { Description = "Password=hunter2 for legacy fallback" };

        var entity = WindowsScheduledTaskScanner.BuildEntity(row, Redactor);

        Assert.DoesNotContain("hunter2", entity.Metadata["Description"]);
        Assert.Equal("true", entity.Metadata["SecretDetected"]);
    }

    [Fact]
    public void BuildEntity_HiddenTask_RecordsHiddenMetadata()
    {
        var row = MakeRow() with { Hidden = true };

        var entity = WindowsScheduledTaskScanner.BuildEntity(row, Redactor);

        Assert.Equal("True", entity.Metadata["Hidden"]);
    }

    [Fact]
    public void BuildEntity_NoFileSystemReader_SkipsPathVerificationWithoutThrowing()
    {
        var entity = WindowsScheduledTaskScanner.BuildEntity(MakeRow(), Redactor);

        Assert.False(entity.Metadata.ContainsKey("ActionExecutableStatus"));
    }
}
