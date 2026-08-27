namespace ServerSleuth.Core.Models;

/// <summary>Windows Service or Linux systemd unit — see skill.md §7, §17.</summary>
public sealed class Service : DiscoveryEntity
{
    public string? DisplayName { get; init; }
    public string? StartType { get; init; }
    public string? ServiceAccount { get; init; }
    public string? ExecutablePath { get; init; }
    public string? CommandLineArguments { get; init; }
    public IReadOnlyList<string> Dependencies { get; init; } = [];
}
