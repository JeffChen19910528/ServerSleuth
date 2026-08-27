namespace ServerSleuth.Windows.ScheduledTasks;

/// <summary>
/// Reads registered tasks from the Task Scheduler. The Mapper (WindowsScheduledTaskScanner's
/// pure BuildEntity) and its tests depend only on this interface and the raw Scheduled*Row
/// DTOs — never on the Task Scheduler COM types directly.
/// </summary>
public interface ITaskSchedulerProvider
{
    TaskSchedulerProbeResult GetSnapshot();
}
