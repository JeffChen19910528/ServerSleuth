using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Common;
using CoreProcess = ServerSleuth.Core.Models.Process;

namespace ServerSleuth.Windows.Process;

/// <summary>
/// Discovers running processes. A single process being inaccessible (protected/system
/// process denying its executable path or command line) never aborts the scanner — it is
/// recorded on that entity and counted toward a Partial status. See skill.md §12, §25-26.
/// </summary>
public sealed class WindowsProcessScanner(
    IProcessEnumerator enumerator,
    IProcessWmiProvider wmiProvider,
    ILogger<WindowsProcessScanner> logger) : IDiscoveryScanner
{
    public string Id => "windows-process-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Windows;

    public Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshots = enumerator.GetSnapshots();
        var wmiInfoByPid = wmiProvider.GetAll();

        var entities = new List<CoreProcess>(snapshots.Count);
        var partialCount = 0;

        foreach (var snapshot in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            wmiInfoByPid.TryGetValue(snapshot.Pid, out var wmiInfo);
            var entity = BuildEntity(snapshot, wmiInfo);
            entities.Add(entity);

            if (IsPartial(snapshot, wmiInfo))
            {
                partialCount++;
            }
        }

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} processes ({Partial} partial)", Id, entities.Count, partialCount);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = partialCount > 0 ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;
        return Task.FromResult(new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities });
    }

    private static bool IsPartial(ProcessSnapshot snapshot, ProcessWmiInfo? wmiInfo) =>
        snapshot.StartTimeAccessDenied || wmiInfo is null || wmiInfo.ExecutablePath is null;

    /// <summary>Pure mapping, unit-testable without a real process/WMI call.</summary>
    internal static CoreProcess BuildEntity(ProcessSnapshot snapshot, ProcessWmiInfo? wmiInfo)
    {
        var entity = new CoreProcess
        {
            Id = $"process:{snapshot.Pid}",
            Name = snapshot.Name,
            Type = "Process",
            Source = EvidenceSources.WindowsProcessApi,
            Status = EntityStatus.Running,
            Confidence = Confidence.VeryHigh(),
            Pid = snapshot.Pid,
            Path = wmiInfo?.ExecutablePath,
            ParentPid = wmiInfo?.ParentProcessId,
            StartTime = snapshot.StartTime,
            CommandLine = wmiInfo?.CommandLine,
            User = wmiInfo is { OwnerDomain: not null, OwnerUser: not null }
                ? $@"{wmiInfo.OwnerDomain}\{wmiInfo.OwnerUser}"
                : null
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.Process, Location = $"PID {snapshot.Pid}", Detail = snapshot.Name });

        if (wmiInfo is not null)
        {
            entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.Command, Location = "WMI Win32_Process" });
        }

        if (snapshot.StartTimeAccessDenied)
        {
            entity.SetMetadata("StartTimeStatus", "AccessDenied");
        }

        if (wmiInfo is null)
        {
            entity.SetMetadata("ExecutablePathStatus", "Unavailable");
            entity.SetMetadata("CommandLineStatus", "Unavailable");
        }
        else if (wmiInfo.ExecutablePath is null)
        {
            entity.SetMetadata("ExecutablePathStatus", "AccessDenied");
        }

        return entity;
    }
}
