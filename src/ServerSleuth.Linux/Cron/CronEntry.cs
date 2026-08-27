namespace ServerSleuth.Linux.Cron;

/// <summary>One parsed cron line's raw facts, before mapping to a domain entity.</summary>
public sealed record CronEntry
{
    public required string Schedule { get; init; }
    public string? User { get; init; }
    public required string Command { get; init; }
}
