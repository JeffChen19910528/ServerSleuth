using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Infrastructure.Configuration;

/// <summary>
/// A directory (or, for an explicitly-referenced file such as a systemd EnvironmentFile, a
/// single file) Configuration Discovery is allowed to look under — always derived from another
/// scanner's already-discovered entity (IIS physical path, Windows Service ImagePath directory,
/// Linux systemd unit ExecStart directory, cron job executable directory, a well-known static
/// technology root), never an arbitrary or user-supplied path. This is what keeps discovery
/// targeted instead of scanning the whole filesystem — see skill.md §4/§29 (Windows), Phase 6E
/// §3-4 (Linux). Moved here from `ServerSleuth.Windows.Configuration` in Phase 6E so Linux
/// configuration discovery can reuse it without a Linux→Windows dependency.
/// </summary>
public sealed record ScanRoot
{
    public required string Path { get; init; }
    public required string Source { get; init; } // "IIS","WindowsService","ScheduledTask","Systemd","Nginx","Apache","Php","MySql","PostgreSql","Docker","Ssh","Cron","ApplicationRoot"
    public string? OwnerEntityId { get; init; }
    public required string Reason { get; init; } // e.g. "IIS PhysicalPath", "Service ExecStart directory"
    public required Confidence Confidence { get; init; }

    /// <summary>When true, <see cref="Path"/> names one specific file to inspect (e.g. an
    /// explicit systemd `EnvironmentFile=` reference) rather than a directory to enumerate.
    /// Added Phase 6E — every prior use of ScanRoot was directory-shaped.</summary>
    public bool IsExplicitFile { get; init; }
}
