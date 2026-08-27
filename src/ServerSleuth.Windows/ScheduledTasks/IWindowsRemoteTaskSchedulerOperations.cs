using ServerSleuth.Core.Targets;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Windows.ScheduledTasks;

/// <summary>
/// The capability boundary a future WinRM transport must satisfy to serve the registered-task
/// tree — see skill.md (Phase 10D-3A) §10, §14. Deliberately parameterless, mirroring
/// <see cref="ITaskSchedulerProvider.GetSnapshot"/>. Returns the SAME
/// <see cref="ScheduledTaskRow"/> list (each already carrying its
/// <see cref="ScheduledTaskActionRow"/>/<see cref="ScheduledTaskTriggerRow"/> children) the
/// local provider already returns — reused directly, not duplicated.
///
/// Exposes no method capable of <c>Run</c>/<c>Stop</c>/<c>Delete</c>/<c>Register</c>/
/// <c>Update</c>/<c>Enable</c>/<c>Disable</c>-ing a task — structurally read-only, and in
/// particular never a capability that could execute a discovered task's own action (skill.md
/// §10's explicit prohibition, matching the existing local scanner's own behavior of only ever
/// reading a task's configured action path/arguments as data, never invoking them).
///
/// No implementation of this interface exists anywhere in this codebase yet (skill.md §3, §18,
/// §27: model only).
/// </summary>
public interface IWindowsRemoteTaskSchedulerOperations
{
    ScanTarget Target { get; }

    WindowsRemoteOperationResult<IReadOnlyList<ScheduledTaskRow>> GetSnapshot();
}
