namespace ServerSleuth.Core.Models;

/// <summary>Linux package manager entry (dpkg/apt, rpm/dnf, apk) — see skill.md §18.</summary>
public sealed class Package : DiscoveryEntity
{
    public string? PackageManager { get; init; } // "dpkg", "rpm", "apk"
}
