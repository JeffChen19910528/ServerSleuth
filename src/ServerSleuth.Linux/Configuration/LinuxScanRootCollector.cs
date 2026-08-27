using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Infrastructure.Configuration;
using ServerSleuth.Linux.Common;
using InfraScanRootCollector = ServerSleuth.Infrastructure.Configuration.ScanRootCollector;

namespace ServerSleuth.Linux.Configuration;

/// <summary>
/// Builds bounded scan roots for Linux configuration discovery — see skill.md (Phase 6E) §3-6.
/// Never a recursive walk of the whole filesystem: every root is either a fixed, well-known
/// technology directory, or derived deterministically from an already-discovered Service
/// (systemd unit) or ScheduledTask (cron job) executable path. Deliberately does NOT add
/// `/etc/cron.d` as a root — Phase 6B's `LinuxScheduledTaskScanner` already owns those files as
/// ScheduledTask entities, and scanning them again here would produce a second, duplicate
/// Configuration entity for the same file with no new information (§4's own text lists
/// `/etc/cron.d` as a candidate root, but §4's header note "do not duplicate Cron job discovery
/// responsibilities from Phase 6B" takes precedence).
/// </summary>
public static class LinuxScanRootCollector
{
    private static readonly (string Path, string Source)[] WellKnownRoots =
    [
        ("/etc/systemd/system", "Systemd"),
        ("/usr/lib/systemd/system", "Systemd"),
        ("/lib/systemd/system", "Systemd"),
        ("/etc/nginx", "Nginx"),
        ("/etc/apache2", "Apache"),
        ("/etc/httpd", "Apache"),
        ("/etc/php", "Php"),
        ("/etc/mysql", "MySql"),
        ("/etc/postgresql", "PostgreSql"),
        ("/etc/docker", "Docker"),
        ("/etc/ssh", "Ssh")
    ];

    public static IReadOnlyList<ScanRoot> Collect(IReadOnlyList<Service> services, IReadOnlyList<ScheduledTask> scheduledTasks)
    {
        var roots = new List<ScanRoot>();

        foreach (var (path, source) in WellKnownRoots)
        {
            roots.Add(new ScanRoot
            {
                Path = path,
                Source = source,
                Reason = $"Well-known {source} configuration directory",
                Confidence = Confidence.Medium() // existence of the directory alone doesn't confirm the technology is actually configured/used
            });
        }

        roots.AddRange(InfraScanRootCollector.CollectFromServices(services, "ApplicationRoot", DeriveApplicationRoot));
        roots.AddRange(InfraScanRootCollector.CollectFromScheduledTasks(scheduledTasks, "ApplicationRoot", IsRooted, DeriveApplicationRoot));

        // Linux paths are case-sensitive — never lower-case them, unlike the Windows collector.
        return InfraScanRootCollector.Deduplicate(roots, StringComparer.Ordinal, p => p.TrimEnd('/'));
    }

    /// <summary>
    /// Derivation rule (documented, deterministic, never guessed): take the directory containing
    /// the executable. If that directory's final path segment is exactly "bin" or "sbin" (the
    /// ubiquitous Linux convention of `/opt/app/bin/executable`), climb exactly one level to the
    /// directory's own parent — UNLESS that parent is "/" itself, since treating the filesystem
    /// root as an "application root" would defeat the entire bounded-scanning principle.
    /// Otherwise, the executable's own directory is used as-is. This never climbs more than one
    /// level, and never climbs for any directory name other than "bin"/"sbin" — see skill.md
    /// (Phase 6E) §5-6 ("do not climb arbitrarily toward /").
    /// </summary>
    internal static string? DeriveApplicationRoot(string? executablePath)
    {
        var directory = LinuxPath.GetDirectoryName(executablePath);
        if (directory is null)
        {
            return null;
        }

        var lastSegment = directory[(directory.LastIndexOf('/') + 1)..];
        if (lastSegment is "bin" or "sbin")
        {
            var parent = LinuxPath.GetDirectoryName(directory);
            if (parent is not null && parent != "/")
            {
                return parent;
            }
        }

        return directory;
    }

    private static bool IsRooted(string path) => path.StartsWith('/');
}
