namespace ServerSleuth.Windows.ScheduledTasks;

/// <summary>
/// Satisfies the SAME <see cref="ITaskSchedulerProvider"/> interface
/// <see cref="WindowsScheduledTaskScanner"/> already depends on — thin adapter over the
/// disclosed-gap <see cref="WinRmTaskSchedulerOperations"/> (see that type's own doc comment).
/// Always returns <see cref="TaskSchedulerProbeResult.Failure"/> — never fabricated task data,
/// never task execution, never a local fallback.
/// </summary>
public sealed class WinRmTaskSchedulerProvider(WinRmTaskSchedulerOperations remoteTaskScheduler) : ITaskSchedulerProvider
{
    public TaskSchedulerProbeResult GetSnapshot()
    {
        var result = remoteTaskScheduler.GetSnapshot();
        return TaskSchedulerProbeResult.Failure(TaskSchedulerAvailability.Failed, result.ErrorMessage ?? "Remote Task Scheduler discovery is not implemented.");
    }
}
