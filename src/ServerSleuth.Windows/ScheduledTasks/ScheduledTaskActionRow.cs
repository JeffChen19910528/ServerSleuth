namespace ServerSleuth.Windows.ScheduledTasks;

public sealed record ScheduledTaskActionRow
{
    public required string Type { get; init; } // "Execute", "ComHandler", "Email", "ShowMessage"
    public string? Path { get; init; }
    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
}
