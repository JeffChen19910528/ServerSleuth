using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Windows.Common;
using CoreApplication = ServerSleuth.Core.Models.Application;
using CoreApplicationPool = ServerSleuth.Core.Models.ApplicationPool;
using CoreWebSite = ServerSleuth.Core.Models.WebSite;

namespace ServerSleuth.Windows.IIS;

/// <summary>
/// Maps IIS sites/applications/bindings/application pools into domain entities. Only the
/// low-level relationship data (skill.md §13: Site HOSTS Application, Application USES
/// ApplicationPool) is recorded here via Application.ComponentEntityIds — no DependencyGraph
/// edges are built; that is Phase 5's job.
/// </summary>
public sealed class IisScanner(IIisConfigurationProvider provider, IFileSystemReader fileSystemReader, ILogger<IisScanner> logger)
    : IDiscoveryScanner
{
    public string Id => "windows-iis-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Windows;

    public Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);
        cancellationToken.ThrowIfCancellationRequested();

        var probe = provider.GetSnapshot();

        var result = probe.Status switch
        {
            IisAvailability.NotInstalled => new DiscoveryResult { ScannerId = Id, Status = ScannerStatus.NotInstalled },
            IisAvailability.AccessDenied => new DiscoveryResult
            {
                ScannerId = Id,
                Status = ScannerStatus.AccessDenied,
                Errors = [new DiscoveryError { ScannerId = Id, Message = probe.ErrorMessage ?? "Access denied.", IsPermissionFailure = true }]
            },
            IisAvailability.Failed => DiscoveryResult.Failure(Id, new DiscoveryError { ScannerId = Id, Message = probe.ErrorMessage ?? "IIS enumeration failed." }),
            _ => BuildResult(probe)
        };

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} entities", Id, result.Entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        return Task.FromResult(result);
    }

    private DiscoveryResult BuildResult(IisProbeResult probe)
    {
        var entities = BuildEntities(probe.Snapshot!, fileSystemReader);

        var errors = probe.PartialFailures
            .Select(failure => new DiscoveryError { ScannerId = Id, Message = failure })
            .ToList();

        var status = errors.Count > 0 ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;

        return new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = errors };
    }

    /// <summary>Pure mapping (aside from the optional physical-path existence check), unit-
    /// testable against a synthetic IisSnapshot without IIS installed.</summary>
    internal static IReadOnlyList<DiscoveryEntity> BuildEntities(IisSnapshot snapshot, IFileSystemReader? fileSystemReader = null)
    {
        var entities = new List<DiscoveryEntity>();
        var poolIdByName = snapshot.ApplicationPools.ToDictionary(p => p.Name, p => $"iis-apppool:{p.Name}");

        foreach (var site in snapshot.Sites)
        {
            entities.Add(BuildWebSite(site, fileSystemReader));

            var siteId = $"iis-site:{site.Name}";
            foreach (var application in site.Applications)
            {
                poolIdByName.TryGetValue(application.ApplicationPoolName ?? string.Empty, out var poolId);
                entities.Add(BuildApplication(site.Name, siteId, application, poolId, fileSystemReader));
            }
        }

        foreach (var pool in snapshot.ApplicationPools)
        {
            entities.Add(BuildApplicationPool(pool));
        }

        return entities;
    }

    private static CoreWebSite BuildWebSite(IisSiteRow site, IFileSystemReader? fileSystemReader)
    {
        var firstBinding = site.Bindings.FirstOrDefault();

        var entity = new CoreWebSite
        {
            Id = $"iis-site:{site.Name}",
            Name = site.Name,
            Type = "WebSite",
            Source = EvidenceSources.IisConfiguration,
            Status = MapSiteStatus(site.State),
            Confidence = Confidence.VeryHigh(),
            PhysicalPath = site.PhysicalPath,
            Bindings = site.Bindings.Select(FormatBinding).ToList(),
            Protocol = firstBinding?.Protocol,
            HostName = firstBinding?.HostName,
            Port = firstBinding?.Port,
            CertificateThumbprint = firstBinding?.CertificateThumbprint
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.IisConfiguration, Location = $"IIS Site: {site.Name}" });
        ApplyPhysicalPathMetadata(entity, site.PhysicalPath, fileSystemReader);

        for (var i = 0; i < site.Bindings.Count; i++)
        {
            var binding = site.Bindings[i];
            if (binding.CertificateThumbprint is not null)
            {
                entity.SetMetadata($"Binding{i}.CertificateThumbprint", binding.CertificateThumbprint);
                entity.SetMetadata($"Binding{i}.CertificateStore", binding.CertificateStoreName ?? string.Empty);
            }
        }

        return entity;
    }

    private static CoreApplication BuildApplication(string siteName, string siteId, IisApplicationRow application, string? poolId, IFileSystemReader? fileSystemReader)
    {
        var displayName = application.VirtualPath == "/" ? siteName : $"{siteName}{application.VirtualPath}";
        var componentIds = poolId is not null ? new[] { siteId, poolId } : [siteId];

        var entity = new CoreApplication
        {
            Id = $"iis-application:{siteName}:{application.VirtualPath}",
            Name = displayName,
            Type = "Application",
            Source = EvidenceSources.IisConfiguration,
            Status = EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Path = application.PhysicalPath,
            ComponentEntityIds = componentIds
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.IisConfiguration, Location = $"IIS Application: {siteName}{application.VirtualPath}" });
        entity.SetMetadata("VirtualPath", application.VirtualPath);
        entity.SetMetadata("SiteName", siteName);

        if (application.ApplicationPoolName is not null)
        {
            entity.SetMetadata("ApplicationPoolName", application.ApplicationPoolName);
        }

        ApplyPhysicalPathMetadata(entity, application.PhysicalPath, fileSystemReader);

        return entity;
    }

    private static CoreApplicationPool BuildApplicationPool(IisAppPoolRow pool)
    {
        var identity = pool.IdentityType == "SpecificUser" ? pool.UserName : pool.IdentityType;

        var entity = new CoreApplicationPool
        {
            Id = $"iis-apppool:{pool.Name}",
            Name = pool.Name,
            Type = "ApplicationPool",
            Source = EvidenceSources.IisConfiguration,
            Status = pool.State == "Started" ? EntityStatus.Running : EntityStatus.Installed,
            Confidence = Confidence.VeryHigh(),
            ManagedRuntimeVersion = string.IsNullOrEmpty(pool.ManagedRuntimeVersion) ? "No Managed Code" : pool.ManagedRuntimeVersion,
            PipelineMode = pool.ManagedPipelineMode,
            Identity = identity,
            StartMode = pool.StartMode,
            Enable32BitAppOnWin64 = pool.Enable32BitAppOnWin64
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.IisConfiguration, Location = $"IIS Application Pool: {pool.Name}" });
        entity.SetMetadata("IdentityType", pool.IdentityType);

        if (pool.IdentityType == "SpecificUser")
        {
            entity.AddTag("MigrationRelevant");
            entity.SetMetadata("MigrationRelevantReason", "Custom application pool identity");
        }

        return entity;
    }

    private static void ApplyPhysicalPathMetadata(DiscoveryEntity entity, string? physicalPath, IFileSystemReader? fileSystemReader)
    {
        if (physicalPath is null)
        {
            entity.SetMetadata("PhysicalPathStatus", "Unavailable");
            return;
        }

        if (fileSystemReader is null)
        {
            return; // pure-mapping tests pass no reader; real scanning always does.
        }

        var infoResult = fileSystemReader.GetFileInfo(physicalPath);
        if (!infoResult.Success)
        {
            entity.SetMetadata("PhysicalPathStatus", infoResult.Status switch
            {
                OperationStatus.AccessDenied => "AccessDenied",
                OperationStatus.NotFound => "NotFound",
                _ => "Unavailable"
            });
        }
    }

    private static EntityStatus MapSiteStatus(string state) => state switch
    {
        "Started" => EntityStatus.Running,
        _ => EntityStatus.Configured
    };

    private static string FormatBinding(IisBindingRow binding)
    {
        var hostSuffix = string.IsNullOrEmpty(binding.HostName) ? string.Empty : $" (host: {binding.HostName})";
        return $"{binding.Protocol}://{binding.IpAddress}:{binding.Port}{hostSuffix}";
    }
}
