using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Infrastructure.Configuration;
using ServerSleuth.Windows.Configuration;

namespace ServerSleuth.Windows.Binaries;

/// <summary>
/// Derives additional scan roots from already-discovered COM registrations' InprocServer32/
/// LocalServer32 paths — reuses Configuration Discovery's ScanRoot type (Phase 4E-1) rather
/// than introducing a parallel model. See skill.md §3, §15.
/// </summary>
public static class ComScanRootCollector
{
    public static IReadOnlyList<ScanRoot> Collect(IReadOnlyList<ComComponent> comComponents)
    {
        var roots = new List<ScanRoot>();

        foreach (var component in comComponents)
        {
            AddIfResolvable(roots, component.InprocServer32, component.Id, "COM InprocServer32 path");
            AddIfResolvable(roots, component.LocalServer32, component.Id, "COM LocalServer32 path");
        }

        return roots
            .GroupBy(r => r.Path.TrimEnd('\\').ToLowerInvariant())
            .Select(g => g.First())
            .ToList();
    }

    private static void AddIfResolvable(List<ScanRoot> roots, string? serverPath, string ownerId, string reason)
    {
        if (serverPath is null)
        {
            return;
        }

        var directory = System.IO.Path.GetDirectoryName(serverPath);
        if (directory is null)
        {
            return;
        }

        roots.Add(new ScanRoot
        {
            Path = directory,
            Source = "COM",
            OwnerEntityId = ownerId,
            Reason = reason,
            Confidence = Confidence.VeryHigh()
        });
    }
}
