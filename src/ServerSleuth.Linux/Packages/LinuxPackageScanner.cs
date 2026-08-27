using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Linux.Common;
using CorePackage = ServerSleuth.Core.Models.Package;

namespace ServerSleuth.Linux.Packages;

/// <summary>
/// Discovers installed packages across every available package manager (dpkg/rpm/apk). Never
/// assumes the distribution from `/etc/os-release`'s `ID` field first — each provider is simply
/// attempted directly, and an unavailable executable degrades to `NotInstalled` for that
/// manager only (skill.md (Phase 6B) §3). Packages from different managers are never merged
/// just because their names match (§8) — deduplication is scoped to a single manager's own
/// results only.
/// </summary>
public sealed class LinuxPackageScanner(IEnumerable<IPackageManagerProvider> providers, ILogger<LinuxPackageScanner> logger)
    : IDiscoveryScanner
{
    public string Id => "linux-package-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Linux;

    public async Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        var entities = new List<CorePackage>();
        var errors = new List<DiscoveryError>();
        var anyDetected = false;
        var anyFailedOrDenied = false;

        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PackageQueryResult result;
            try
            {
                result = await provider.QueryInstalledPackagesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Package provider {Provider} threw unexpectedly", provider.PackageManagerName);
                result = new PackageQueryResult { Status = PackageManagerAvailability.Failed, ErrorMessage = ex.Message };
            }

            switch (result.Status)
            {
                case PackageManagerAvailability.NotInstalled:
                    continue; // a missing package manager is a normal, expected outcome
                case PackageManagerAvailability.AccessDenied:
                    anyFailedOrDenied = true;
                    errors.Add(new DiscoveryError { ScannerId = Id, Message = $"{provider.PackageManagerName}: {result.ErrorMessage}", IsPermissionFailure = true });
                    continue;
                case PackageManagerAvailability.Failed:
                    anyFailedOrDenied = true;
                    errors.Add(new DiscoveryError { ScannerId = Id, Message = $"{provider.PackageManagerName}: {result.ErrorMessage}" });
                    continue;
            }

            anyDetected = true;
            entities.AddRange(Deduplicate(result.Packages, provider.PackageManagerName).Select(row => BuildEntity(row, provider.PackageManagerName)));
        }

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} packages", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = anyFailedOrDenied
            ? ScannerStatus.PartiallySupported
            : anyDetected ? ScannerStatus.Supported : ScannerStatus.NotInstalled;

        return new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = errors };
    }

    /// <summary>Deduplicates by (Name, Version, Architecture) within one manager's own result
    /// set — never across managers. See skill.md §8.</summary>
    private static IEnumerable<PackageRow> Deduplicate(IReadOnlyList<PackageRow> packages, string packageManager)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in packages)
        {
            var key = BuildId(packageManager, row);
            if (seen.Add(key))
            {
                yield return row;
            }
        }
    }

    private static string BuildId(string packageManager, PackageRow row) =>
        $"package:{packageManager}:{row.Name}:{row.Version ?? "unknown-version"}:{row.Architecture ?? "unknown-arch"}";

    /// <summary>Pure mapping, unit-testable against a synthetic PackageRow.</summary>
    internal static CorePackage BuildEntity(PackageRow row, string packageManager)
    {
        var entity = new CorePackage
        {
            Id = BuildId(packageManager, row),
            Name = row.Name,
            Type = "Package",
            Source = EvidenceSources.PackageManager,
            Status = EntityStatus.Installed,
            Confidence = Confidence.VeryHigh(),
            Version = row.Version,
            Publisher = row.Maintainer,
            Description = row.Description,
            Path = row.InstallPath,
            PackageManager = packageManager
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.PackageManager, Location = packageManager, Detail = row.Name });

        if (row.Architecture is not null) entity.SetMetadata("Architecture", row.Architecture);
        if (row.SourcePackage is not null) entity.SetMetadata("SourcePackage", row.SourcePackage);

        return entity;
    }
}
