using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Infrastructure.Configuration;
using InfraScanRootCollector = ServerSleuth.Infrastructure.Configuration.ScanRootCollector;

namespace ServerSleuth.Windows.Configuration;

/// <summary>
/// Builds scan roots from entities other scanners already discovered — never from a
/// user-supplied or arbitrary path. Delegates the Service/ScheduledTask-derived roots (shared
/// with Linux since both platforms produce the same Core entity types) to the Phase 6E-extracted
/// <see cref="ServerSleuth.Infrastructure.Configuration.ScanRootCollector"/>, and adds Windows's
/// own IIS-derived roots plus Windows's case-insensitive path deduplication. See skill.md §4-5,
/// §29.
/// </summary>
public static class ScanRootCollector
{
    public static IReadOnlyList<ScanRoot> Collect(
        IReadOnlyList<WebSite> sites,
        IReadOnlyList<Application> applications,
        IReadOnlyList<Service> services,
        IReadOnlyList<ScheduledTask> scheduledTasks)
    {
        var roots = new List<ScanRoot>();

        foreach (var site in sites.Where(s => s.PhysicalPath is not null))
        {
            roots.Add(new ScanRoot
            {
                Path = site.PhysicalPath!,
                Source = "IIS",
                OwnerEntityId = site.Id,
                Reason = "IIS Site PhysicalPath",
                Confidence = Confidence.VeryHigh()
            });
        }

        foreach (var application in applications.Where(a => a.Path is not null))
        {
            roots.Add(new ScanRoot
            {
                Path = application.Path!,
                Source = "IIS",
                OwnerEntityId = application.Id,
                Reason = "IIS Application PhysicalPath",
                Confidence = Confidence.VeryHigh()
            });
        }

        roots.AddRange(InfraScanRootCollector.CollectFromServices(services, "WindowsService", System.IO.Path.GetDirectoryName));
        roots.AddRange(InfraScanRootCollector.CollectFromScheduledTasks(scheduledTasks, "ScheduledTask", System.IO.Path.IsPathRooted, System.IO.Path.GetDirectoryName));

        return InfraScanRootCollector.Deduplicate(roots, StringComparer.OrdinalIgnoreCase, p => p.TrimEnd('\\').ToLowerInvariant());
    }
}
