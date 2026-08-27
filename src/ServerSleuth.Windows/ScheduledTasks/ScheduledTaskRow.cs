namespace ServerSleuth.Windows.ScheduledTasks;

/// <summary>Raw data read from one registered task via the Task Scheduler 2.0 COM API.
/// Fields that could not be read are null, never an empty string standing in for unknown.</summary>
public sealed record ScheduledTaskRow
{
    public required string Path { get; init; } // full path, e.g. "\Microsoft\Windows\UpdateOrchestrator\Schedule Scan"
    public required string Name { get; init; } // leaf name only
    public required bool Enabled { get; init; }
    public required string State { get; init; } // "Ready","Disabled","Queued","Running","Unknown"
    public bool Hidden { get; init; }
    public string? Author { get; init; }
    public string? Description { get; init; }
    public string? RunLevel { get; init; } // "LeastPrivilege","HighestAvailable"
    public string? UserId { get; init; } // principal account name only, never a credential
    public DateTimeOffset? LastRunTime { get; init; }
    public DateTimeOffset? NextRunTime { get; init; }
    public int? LastTaskResult { get; init; }
    public string? ExecutionTimeLimit { get; init; } // raw ISO8601 duration string
    public IReadOnlyList<ScheduledTaskActionRow> Actions { get; init; } = [];
    public IReadOnlyList<ScheduledTaskTriggerRow> Triggers { get; init; } = [];
}
