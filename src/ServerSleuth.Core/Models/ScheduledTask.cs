namespace ServerSleuth.Core.Models;

/// <summary>Windows Task Scheduler task, or Linux cron/systemd timer — see skill.md §14.</summary>
public sealed class ScheduledTask : DiscoveryEntity
{
    public string? Folder { get; init; }
    public string? Trigger { get; init; }
    public DateTimeOffset? NextRun { get; init; }
    public string? Action { get; init; }
    public string? RunAsAccount { get; init; }
    public bool Enabled { get; init; }
}
