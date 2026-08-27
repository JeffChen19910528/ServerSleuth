namespace ServerSleuth.Core.Models;

public sealed class Process : DiscoveryEntity
{
    public int Pid { get; init; }
    public int? ParentPid { get; init; }
    public string? CommandLine { get; init; }
    public string? User { get; init; }
    public DateTimeOffset? StartTime { get; init; }
}
