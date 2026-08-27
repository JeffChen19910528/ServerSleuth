using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Linux.Common;
using CoreProcess = ServerSleuth.Core.Models.Process;

namespace ServerSleuth.Linux.Process;

/// <summary>
/// Discovers running processes from `/proc`. A single inaccessible, malformed, or
/// mid-scan-exited process entry is recorded on that entity and counted toward a Partial
/// status — it never aborts the scanner. See skill.md (Phase 6A) §4, §12.
/// </summary>
public sealed class LinuxProcessScanner(IProcProvider provider, ILogger<LinuxProcessScanner> logger) : IDiscoveryScanner
{
    public string Id => "linux-process-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Linux;

    public Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshots = provider.GetProcessSnapshots();
        var entities = new List<CoreProcess>(snapshots.Count);
        var partialCount = 0;

        foreach (var snapshot in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            entities.Add(BuildEntity(snapshot));

            if (snapshot.AccessDenied || snapshot.MalformedEntry || snapshot.ExecutablePath is null)
            {
                partialCount++;
            }
        }

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} processes ({Partial} partial)", Id, entities.Count, partialCount);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = partialCount > 0 ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;
        return Task.FromResult(new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities });
    }

    /// <summary>Pure mapping, unit-testable against a synthetic snapshot without a real /proc.</summary>
    internal static CoreProcess BuildEntity(ProcProcessSnapshot snapshot)
    {
        var entity = new CoreProcess
        {
            Id = $"process:{snapshot.Pid}",
            Name = snapshot.Name ?? $"pid-{snapshot.Pid}",
            Type = "Process",
            Source = EvidenceSources.ProcFilesystem,
            Status = EntityStatus.Running,
            Confidence = snapshot.AccessDenied || snapshot.MalformedEntry ? Confidence.Low() : Confidence.VeryHigh(),
            Pid = snapshot.Pid,
            ParentPid = snapshot.ParentPid,
            Path = snapshot.ExecutablePath,
            CommandLine = snapshot.CommandLine
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.FileSystem, Location = $"/proc/{snapshot.Pid}/status", Detail = snapshot.Name });

        if (snapshot.ExecutablePath is not null)
        {
            entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.FileSystem, Location = $"/proc/{snapshot.Pid}/exe" });
        }

        if (snapshot.AccessDenied)
        {
            entity.SetMetadata("AccessStatus", "AccessDenied");
        }
        else if (snapshot.MalformedEntry)
        {
            entity.SetMetadata("AccessStatus", "MalformedEntry");
        }
        else
        {
            if (snapshot.ExecutablePath is null)
            {
                entity.SetMetadata("ExecutablePathStatus", "Unavailable"); // zombie, kernel thread, or exited mid-scan
            }

            if (snapshot.State is not null)
            {
                entity.SetMetadata("State", snapshot.State);
            }

            if (snapshot.Uid is not null)
            {
                entity.SetMetadata("Uid", snapshot.Uid);
            }
        }

        // Username resolution (/etc/passwd lookup) is deliberately out of scope for Phase 6A —
        // only /etc/os-release, /proc/*, and systemd metadata are read this phase (skill.md §11).
        return entity;
    }
}
