using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Linux.Common;
using CoreOperatingSystem = ServerSleuth.Core.Models.OperatingSystem;

namespace ServerSleuth.Linux.OperatingSystem;

/// <summary>
/// Discovers the current machine's Linux distribution/kernel/architecture identity. Primary
/// source is `/etc/os-release`; `/proc/sys/kernel/*` is used as a secondary/fallback source so
/// a missing or malformed os-release never blocks discovery entirely. `uname -m` (fixed
/// executable, fixed arguments, no shell) is used only for architecture, since it is not
/// otherwise available from a file. See skill.md (Phase 6A) §3.
/// </summary>
public sealed class LinuxOsScanner(
    IFileSystemReader fileSystemReader,
    IProcessRunner processRunner,
    ILogger<LinuxOsScanner> logger) : IDiscoveryScanner
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);

    public string Id => "linux-os-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Linux;

    public async Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        var osReleaseResult = await fileSystemReader.ReadTextAsync("/etc/os-release", cancellationToken);
        var hostnameResult = await fileSystemReader.ReadTextAsync("/proc/sys/kernel/hostname", cancellationToken);
        var releaseResult = await fileSystemReader.ReadTextAsync("/proc/sys/kernel/osrelease", cancellationToken);
        var typeResult = await fileSystemReader.ReadTextAsync("/proc/sys/kernel/ostype", cancellationToken);

        string? unameMachine = null;
        var unameResult = await processRunner.RunAsync(
            new ProcessRequest { Executable = "uname", Arguments = ["-m"], Timeout = CommandTimeout },
            cancellationToken);
        if (unameResult.Success)
        {
            unameMachine = unameResult.StandardOutput.Trim();
        }

        var snapshot = new LinuxOsSnapshot
        {
            OsReleaseAvailable = osReleaseResult.Success,
            OsRelease = osReleaseResult.Success ? OsReleaseParser.Parse(osReleaseResult.Value!) : new Dictionary<string, string>(),
            Hostname = hostnameResult.Success ? hostnameResult.Value!.Trim() : null,
            KernelRelease = releaseResult.Success ? releaseResult.Value!.Trim() : null,
            OsType = typeResult.Success ? typeResult.Value!.Trim() : null,
            UnameMachine = unameMachine
        };

        var entities = BuildEntities(snapshot);

        var errors = new List<DiscoveryError>();
        if (!osReleaseResult.Success)
        {
            errors.Add(new DiscoveryError { ScannerId = Id, Message = $"/etc/os-release: {osReleaseResult.Status}", IsPermissionFailure = osReleaseResult.Status == OperationStatus.AccessDenied });
        }

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} entities", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = errors.Count == 0 ? ScannerStatus.Supported : ScannerStatus.PartiallySupported;
        return new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = errors };
    }

    /// <summary>Pure mapping, unit-testable against a synthetic snapshot without real files or
    /// process execution.</summary>
    internal static IReadOnlyList<DiscoveryEntity> BuildEntities(LinuxOsSnapshot snapshot)
    {
        var hostname = snapshot.Hostname ?? snapshot.OsRelease.GetValueOrDefault("PRETTY_NAME") ?? "unknown-host";

        var server = new Server
        {
            Id = $"server:{hostname}",
            Name = hostname,
            Type = "Server",
            Source = snapshot.Hostname is not null ? EvidenceSources.ProcSysKernel : EvidenceSources.OsRelease,
            Status = EntityStatus.Running,
            Confidence = snapshot.Hostname is not null ? Confidence.VeryHigh() : Confidence.Medium(),
            Hostname = snapshot.Hostname
        };

        if (snapshot.Hostname is not null)
        {
            server.AddEvidence(new EvidenceRecord { Type = EvidenceType.FileSystem, Location = "/proc/sys/kernel/hostname" });
        }

        var platform = snapshot.OsRelease.GetValueOrDefault("PRETTY_NAME") ?? snapshot.OsRelease.GetValueOrDefault("NAME");

        var os = new CoreOperatingSystem
        {
            Id = $"os:{hostname}",
            Name = platform ?? snapshot.OsType ?? "Linux",
            Type = "OperatingSystem",
            Source = snapshot.OsReleaseAvailable ? EvidenceSources.OsRelease : EvidenceSources.ProcSysKernel,
            Status = EntityStatus.Running,
            Confidence = snapshot.OsReleaseAvailable ? Confidence.VeryHigh() : Confidence.Medium(),
            Architecture = LinuxArchitectureMapper.FromUname(snapshot.UnameMachine),
            Platform = platform,
            Version = snapshot.OsRelease.GetValueOrDefault("VERSION_ID"),
            Kernel = snapshot.KernelRelease
        };

        if (snapshot.OsReleaseAvailable)
        {
            os.AddEvidence(new EvidenceRecord { Type = EvidenceType.FileSystem, Location = "/etc/os-release" });
        }

        if (snapshot.KernelRelease is not null)
        {
            os.AddEvidence(new EvidenceRecord { Type = EvidenceType.FileSystem, Location = "/proc/sys/kernel/osrelease" });
        }

        if (snapshot.UnameMachine is not null)
        {
            os.AddEvidence(new EvidenceRecord { Type = EvidenceType.Command, Location = "uname -m", Detail = snapshot.UnameMachine });
        }
        else
        {
            os.SetMetadata("ArchitectureStatus", "Unavailable");
        }

        if (snapshot.OsRelease.TryGetValue("ID", out var distroId))
        {
            os.SetMetadata("DistributionId", distroId);
        }

        return [server, os];
    }
}
