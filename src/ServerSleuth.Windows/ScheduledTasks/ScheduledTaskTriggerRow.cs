namespace ServerSleuth.Windows.ScheduledTasks;

public sealed record ScheduledTaskTriggerRow
{
    public required string Type { get; init; } // "Time","Daily","Weekly","Monthly","Boot","Logon","Idle","Event","Registration","SessionStateChange","Unknown"
    public bool Enabled { get; init; }
    public string? StartBoundary { get; init; }
    public string? EndBoundary { get; init; }
}
