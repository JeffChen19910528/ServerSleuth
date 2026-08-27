using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Linux.Common;
using CoreService = ServerSleuth.Core.Models.Service;

namespace ServerSleuth.Linux.Systemd;

/// <summary>
/// Discovers systemd service units — read-only: never `systemctl start/stop/restart/enable/
/// disable/reload`, never a unit-file write. See skill.md (Phase 6A) §7-9.
/// </summary>
public sealed class LinuxSystemdServiceScanner(ISystemdProvider provider, ILogger<LinuxSystemdServiceScanner> logger)
    : IDiscoveryScanner
{
    public string Id => "linux-systemd-service-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Linux;

    public Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);
        cancellationToken.ThrowIfCancellationRequested();

        var probe = provider.GetSnapshot();

        var result = probe.Status switch
        {
            SystemdAvailability.NotInstalled => new DiscoveryResult { ScannerId = Id, Status = ScannerStatus.NotInstalled },
            SystemdAvailability.AccessDenied => new DiscoveryResult
            {
                ScannerId = Id,
                Status = ScannerStatus.AccessDenied,
                Errors = [new DiscoveryError { ScannerId = Id, Message = probe.ErrorMessage ?? "Access denied.", IsPermissionFailure = true }]
            },
            SystemdAvailability.Failed => DiscoveryResult.Failure(Id, new DiscoveryError { ScannerId = Id, Message = probe.ErrorMessage ?? "systemd enumeration failed." }),
            _ => BuildResult(probe)
        };

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} services", Id, result.Entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        return Task.FromResult(result);
    }

    private DiscoveryResult BuildResult(SystemdProbeResult probe)
    {
        var entities = probe.Units.Select(BuildEntity).ToList();
        var errors = probe.PartialFailures.Select(f => new DiscoveryError { ScannerId = Id, Message = f, IsPermissionFailure = true }).ToList();
        var status = errors.Count > 0 ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;

        return new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = errors };
    }

    /// <summary>Pure mapping, unit-testable against a synthetic SystemdUnitRow.</summary>
    internal static CoreService BuildEntity(SystemdUnitRow row)
    {
        var executablePath = ExecStartParser.ExtractExecutablePath(row.ExecStart);

        var entity = new CoreService
        {
            Id = $"service:{row.UnitName}",
            Name = row.UnitName,
            Type = "Service",
            Source = EvidenceSources.Systemd,
            Status = MapStatus(row),
            Confidence = row.DetailUnavailable ? Confidence.Medium() : Confidence.VeryHigh(),
            DisplayName = row.Description,
            StartType = row.UnitFileState,
            ServiceAccount = row.User,
            ExecutablePath = executablePath,
            CommandLineArguments = row.ExecStart
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.ServiceConfiguration, Location = "systemd", Detail = row.UnitName });

        if (row.FragmentPath is not null)
        {
            entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.ConfigurationFile, Location = row.FragmentPath });
            entity.SetMetadata("UnitFilePath", row.FragmentPath);
        }

        if (row.LoadState is not null) entity.SetMetadata("LoadState", row.LoadState);
        if (row.ActiveState is not null) entity.SetMetadata("ActiveState", row.ActiveState);
        if (row.SubState is not null) entity.SetMetadata("SubState", row.SubState);
        if (row.WorkingDirectory is not null) entity.SetMetadata("WorkingDirectory", row.WorkingDirectory);

        if (row.DetailUnavailable)
        {
            entity.SetMetadata("DetailStatus", "Unavailable");
        }
        else if (executablePath is null && row.ExecStart is not null)
        {
            entity.SetMetadata("ExecutablePathStatus", "Unrecognized ExecStart shape");
        }

        return entity;
    }

    private static EntityStatus MapStatus(SystemdUnitRow row)
    {
        if (row.ActiveState == "active")
        {
            return EntityStatus.Running;
        }

        return row.LoadState switch
        {
            "loaded" => EntityStatus.Configured,
            "not-found" => EntityStatus.Unknown,
            _ => EntityStatus.Unknown
        };
    }
}
