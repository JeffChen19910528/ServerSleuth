namespace ServerSleuth.Windows.ScheduledTasks;

public sealed record TaskSchedulerProbeResult
{
    public required TaskSchedulerAvailability Status { get; init; }
    public IReadOnlyList<ScheduledTaskRow> Tasks { get; init; } = [];
    public IReadOnlyList<string> PartialFailures { get; init; } = [];
    public string? ErrorMessage { get; init; }

    public static TaskSchedulerProbeResult Available(IReadOnlyList<ScheduledTaskRow> tasks, IReadOnlyList<string>? partialFailures = null) => new()
    {
        Status = TaskSchedulerAvailability.Available,
        Tasks = tasks,
        PartialFailures = partialFailures ?? []
    };

    public static TaskSchedulerProbeResult Failure(TaskSchedulerAvailability status, string errorMessage) => new()
    {
        Status = status,
        ErrorMessage = errorMessage
    };
}
