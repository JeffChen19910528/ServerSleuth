namespace ServerSleuth.Core.Models;

/// <summary>Windows installed-software entry — see skill.md §10.</summary>
public sealed class Software : DiscoveryEntity
{
    public string? InstallLocation { get; init; }
    public DateTimeOffset? InstallDate { get; init; }
    public string? UninstallCommand { get; init; }
}
