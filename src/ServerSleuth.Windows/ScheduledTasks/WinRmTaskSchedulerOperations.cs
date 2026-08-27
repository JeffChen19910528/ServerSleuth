using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Windows.ScheduledTasks;

/// <summary>
/// A DISCLOSED capability gap (skill.md Phase 10D-3B §14, §34) — the Task Scheduler 2.0 COM API
/// the LOCAL <see cref="TaskSchedulerProvider"/> uses has no WS-Man/WMI-reachable equivalent:
/// there is no maintained WMI provider for Task Scheduler 2.0 tasks (the only WMI-visible
/// scheduling class, <c>Win32_ScheduledJob</c>, covers only the long-deprecated <c>AT</c>
/// command's jobs, not modern Task Scheduler tasks), and the COM API itself has no remote/WS-Man
/// binding — only local or DCOM. No safe structured path exists without PowerShell
/// (<c>Get-ScheduledTask</c>) or <c>schtasks.exe</c> text parsing, both of which this phase's
/// own §4 forbids as a general capability surface. Every call returns
/// <see cref="TaskSchedulerAvailability.Failed"/> with a diagnostic — never fabricated task
/// data, never a PowerShell fallback, never task EXECUTION of any kind (the interface has no
/// method that could execute one even if a transport existed).
/// </summary>
public sealed class WinRmTaskSchedulerOperations(ScanTarget target) : IWindowsRemoteTaskSchedulerOperations
{
    public ScanTarget Target { get; } = target;

    public WindowsRemoteOperationResult<IReadOnlyList<ScheduledTaskRow>> GetSnapshot() =>
        WindowsRemoteOperationResult<IReadOnlyList<ScheduledTaskRow>>.Failure(
            OperationStatus.NotInstalled,
            "Remote Task Scheduler discovery is not implemented in Phase 10D-3B: Task Scheduler 2.0 has no " +
            "WS-Man/WMI-reachable structured API (Win32_ScheduledJob only covers the deprecated AT command). " +
            "Disclosed gap, not a PowerShell workaround — see ARCHITECTURE.md's Phase 10D-3B addendum.");
}
