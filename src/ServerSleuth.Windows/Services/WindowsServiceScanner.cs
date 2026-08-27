using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Common;
using ServerSleuth.Windows.Registry;
using CoreService = ServerSleuth.Core.Models.Service;

namespace ServerSleuth.Windows.Services;

/// <summary>
/// The most important Phase 3 scanner: maps every Win32 service to its executable, start
/// account, dependencies and start type — not just Get-Service's name/status. See skill.md §7.
/// </summary>
public sealed class WindowsServiceScanner(
    IServiceEnumerator enumerator,
    IWindowsRegistryReader registryReader,
    ILogger<WindowsServiceScanner> logger) : IDiscoveryScanner
{
    private static readonly HashSet<string> BuiltInAccounts = new(StringComparer.OrdinalIgnoreCase)
    {
        "LocalSystem",
        "NT AUTHORITY\\LocalService",
        "NT AUTHORITY\\NetworkService",
        "NT AUTHORITY\\SYSTEM"
    };

    public string Id => "windows-service-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Windows;

    public Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshots = enumerator.GetSnapshots();
        var entities = new List<CoreService>(snapshots.Count);
        var partialCount = 0;

        foreach (var snapshot in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detail = ServiceRegistryDetailReader.Read(registryReader, snapshot.ServiceName);
            entities.Add(BuildEntity(snapshot, detail));

            if (detail.ImagePath is null)
            {
                partialCount++;
            }
        }

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} services ({Partial} partial)", Id, entities.Count, partialCount);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = partialCount > 0 ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;
        return Task.FromResult(new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities });
    }

    /// <summary>Pure mapping, unit-testable without a real SCM/registry call.</summary>
    internal static CoreService BuildEntity(ServiceSnapshot snapshot, ServiceRegistryDetail detail)
    {
        var entity = new CoreService
        {
            Id = $"service:{snapshot.ServiceName}",
            Name = snapshot.ServiceName,
            Type = "Service",
            Source = EvidenceSources.ServiceControlManager,
            Status = MapStatus(snapshot.Status),
            Confidence = Confidence.VeryHigh(),
            DisplayName = snapshot.DisplayName,
            StartType = MapStartMode(detail.StartMode, detail.DelayedAutoStart),
            ServiceAccount = detail.ObjectName,
            ExecutablePath = detail.ImagePath,
            Dependencies = detail.DependOnService
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.ServiceConfiguration, Location = "Windows Service Control Manager", Detail = snapshot.ServiceName });

        if (detail.ImagePath is not null)
        {
            entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.Registry, Location = $@"HKLM\SYSTEM\CurrentControlSet\Services\{snapshot.ServiceName}" });
        }
        else
        {
            entity.SetMetadata("ExecutablePathStatus", "Unavailable");
        }

        if (detail.ServiceDll is not null)
        {
            entity.SetMetadata("ServiceDll", detail.ServiceDll);
        }

        if (detail.HasRecoveryConfiguration)
        {
            entity.SetMetadata("HasRecoveryConfiguration", "true");
        }

        if (detail.ObjectName is not null && !BuiltInAccounts.Contains(detail.ObjectName))
        {
            entity.AddTag("MigrationRelevant");
            entity.SetMetadata("MigrationRelevantReason", "Custom service account");
        }

        return entity;
    }

    private static EntityStatus MapStatus(string status) => status switch
    {
        "Running" => EntityStatus.Running,
        _ => EntityStatus.Installed
    };

    private static string? MapStartMode(int? startMode, bool? delayedAutoStart) => startMode switch
    {
        0 => "Boot",
        1 => "System",
        2 => delayedAutoStart == true ? "Automatic (Delayed Start)" : "Automatic",
        3 => "Manual",
        4 => "Disabled",
        _ => null
    };
}
