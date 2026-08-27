using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Common;
using ServerSleuth.Windows.Registry;
using CoreSoftware = ServerSleuth.Core.Models.Software;

namespace ServerSleuth.Windows.Software;

/// <summary>
/// Enumerates all three Uninstall registry locations (64-bit, WOW6432Node, per-user). Every
/// registry key becomes its own entity — no cross-source merging happens here, since deciding
/// whether two entries are "the same" logical software belongs to Correlation (Phase 5), not
/// this scanner. See skill.md §10, §32 and the Phase 3 ARCHITECTURE.md addendum.
/// </summary>
public sealed class WindowsInstalledSoftwareScanner(IWindowsRegistryReader registryReader, ILogger<WindowsInstalledSoftwareScanner> logger)
    : IDiscoveryScanner
{
    public string Id => "windows-installed-software-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Windows;

    public Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        var entities = new List<CoreSoftware>();
        var deniedSources = 0;

        foreach (var source in SoftwareRegistrySource.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subKeyNames = registryReader.GetSubKeyNames(source.Hive, source.View, source.Path);
            if (!subKeyNames.Success)
            {
                deniedSources++;
                logger.LogWarning(ScannerLogEvents.PermissionDenied, "{ScannerId} could not enumerate {Source}: {Status}", Id, source.Label, subKeyNames.Status);
                continue;
            }

            foreach (var subKeyName in subKeyNames.Value!)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var values = registryReader.GetValues(source.Hive, source.View, $"{source.Path}\\{subKeyName}");
                if (!values.Success || values.Value is null)
                {
                    continue;
                }

                if (SoftwareRegistryRowBuilder.TryBuild(subKeyName, values.Value, out var row))
                {
                    entities.Add(BuildEntity(row, source));
                }
            }
        }

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} software entries", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = deniedSources switch
        {
            0 => ScannerStatus.Supported,
            var n when n < SoftwareRegistrySource.All.Count => ScannerStatus.PartiallySupported,
            _ => ScannerStatus.AccessDenied
        };

        return Task.FromResult(new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities });
    }

    /// <summary>Pure mapping, unit-testable without touching the registry.</summary>
    internal static CoreSoftware BuildEntity(SoftwareRegistryRow row, SoftwareRegistrySource source)
    {
        var registryPath = $@"{source.Label}\{row.RegistryKeyName}";

        var entity = new CoreSoftware
        {
            Id = $"software:{registryPath}",
            Name = row.DisplayName,
            Type = "Software",
            Source = EvidenceSources.WindowsRegistry,
            Status = EntityStatus.Installed,
            Confidence = Confidence.VeryHigh(),
            Version = row.DisplayVersion,
            Publisher = row.Publisher,
            Architecture = source.ArchitectureHint,
            InstallLocation = row.InstallLocation,
            InstallDate = row.InstallDate,
            UninstallCommand = row.UninstallString
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.Registry, Location = registryPath });

        return entity;
    }
}
