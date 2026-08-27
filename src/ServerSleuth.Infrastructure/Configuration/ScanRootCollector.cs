using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Infrastructure.Configuration;

/// <summary>
/// Builds scan roots from entities other scanners already discovered — never from a
/// user-supplied or arbitrary path. Only the platform-neutral pieces live here (both Windows
/// Services and Linux systemd units are `Core.Models.Service`; both Windows Scheduled Tasks and
/// Linux cron jobs are `Core.Models.ScheduledTask`); each platform composes these with its own
/// platform-specific roots (Windows: IIS; Linux: well-known technology directories) and its own
/// path-comparer (Windows paths are case-insensitive, Linux paths are not — see skill.md §29
/// (Windows), Phase 6E §27 (Linux)). Moved here from `ServerSleuth.Windows.Configuration` in
/// Phase 6E so Linux configuration discovery can reuse it without a Linux→Windows dependency.
/// </summary>
public static class ScanRootCollector
{
    public static IEnumerable<ScanRoot> CollectFromServices(IReadOnlyList<Service> services, string source, Func<string, string?> getDirectory) =>
        services
            .Where(s => s.ExecutablePath is not null)
            .Select(s => (Service: s, Directory: getDirectory(s.ExecutablePath!)))
            .Where(t => t.Directory is not null)
            .Select(t => new ScanRoot
            {
                Path = t.Directory!,
                Source = source,
                OwnerEntityId = t.Service.Id,
                Reason = "Service ExecutablePath directory",
                Confidence = Confidence.High()
            });

    public static IEnumerable<ScanRoot> CollectFromScheduledTasks(IReadOnlyList<ScheduledTask> scheduledTasks, string source, Func<string, bool> isPathRooted, Func<string, string?> getDirectory) =>
        scheduledTasks
            .Where(t => t.Action is not null && isPathRooted(t.Action))
            .Select(t => (Task: t, Directory: getDirectory(t.Action!)))
            .Where(t => t.Directory is not null)
            .Select(t => new ScanRoot
            {
                Path = t.Directory!,
                Source = source,
                OwnerEntityId = t.Task.Id,
                Reason = "Scheduled Task action executable directory",
                Confidence = Confidence.High()
            });

    /// <summary>Deduplicates by normalized path (using the caller-supplied comparer and,
    /// optionally, a caller-supplied normalization such as trailing-separator trimming) so the
    /// same directory is never scanned twice even if two owners point at it — the first
    /// occurrence wins.</summary>
    public static IReadOnlyList<ScanRoot> Deduplicate(IEnumerable<ScanRoot> roots, StringComparer comparer, Func<string, string>? normalize = null)
    {
        normalize ??= p => p;
        return roots.GroupBy(r => normalize(r.Path), comparer).Select(g => g.First()).ToList();
    }
}
