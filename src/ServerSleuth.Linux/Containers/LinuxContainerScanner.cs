using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Security;

namespace ServerSleuth.Linux.Containers;

/// <summary>
/// Discovers containers, images, named volumes, and networks across every available container
/// runtime (Docker, Podman). Read-only throughout — never `run`/`exec`/`start`/`stop`/`rm`/
/// `pull`/`push`/`build`/`commit`, never a shell. See skill.md (Phase 6C) §1-4. Reuses
/// `Core.Models.Container` (Phase 1, unchanged) for all four entity kinds via its `Type`
/// discriminator — never a runtime-specific `DockerContainer`/`PodmanContainer` type.
/// </summary>
public sealed class LinuxContainerScanner(
    IEnumerable<IContainerRuntimeProvider> providers,
    ISecretRedactor secretRedactor,
    ILogger<LinuxContainerScanner> logger) : IDiscoveryScanner
{
    public string Id => "linux-container-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Linux;

    public async Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        var entities = new List<Container>();
        var errors = new List<DiscoveryError>();
        var anyDetected = false;
        var anyPartialOrDenied = false;

        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ContainerRuntimeSnapshot snapshot;
            try
            {
                snapshot = await provider.GetSnapshotAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Container runtime provider {Provider} threw unexpectedly", provider.RuntimeName);
                snapshot = new ContainerRuntimeSnapshot { Status = ContainerRuntimeAvailability.Unavailable, ErrorMessage = ex.Message };
            }

            switch (snapshot.Status)
            {
                case ContainerRuntimeAvailability.NotInstalled:
                    continue; // a missing runtime is a normal, expected outcome
                case ContainerRuntimeAvailability.AccessDenied:
                    anyPartialOrDenied = true;
                    errors.Add(new DiscoveryError { ScannerId = Id, Message = $"{provider.RuntimeName}: {snapshot.ErrorMessage}", IsPermissionFailure = true });
                    continue;
                case ContainerRuntimeAvailability.Unavailable:
                    anyPartialOrDenied = true;
                    errors.Add(new DiscoveryError { ScannerId = Id, Message = $"{provider.RuntimeName}: {snapshot.ErrorMessage}" });
                    continue;
                case ContainerRuntimeAvailability.PartiallySupported:
                    anyPartialOrDenied = true;
                    foreach (var failure in snapshot.PartialFailures)
                    {
                        errors.Add(new DiscoveryError { ScannerId = Id, Message = $"{provider.RuntimeName}: {failure}" });
                    }
                    break;
            }

            anyDetected = true;

            entities.AddRange(snapshot.Containers.Select(row => BuildContainerEntity(row, provider.RuntimeName, secretRedactor)));
            entities.AddRange(snapshot.Images.Select(row => BuildImageEntity(row, provider.RuntimeName)));
            entities.AddRange(snapshot.Volumes.Select(row => BuildVolumeEntity(row, provider.RuntimeName, secretRedactor)));
            entities.AddRange(snapshot.Networks.Select(row => BuildNetworkEntity(row, provider.RuntimeName)));
        }

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} entities", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = anyPartialOrDenied
            ? ScannerStatus.PartiallySupported
            : anyDetected ? ScannerStatus.Supported : ScannerStatus.NotInstalled;

        return new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = errors };
    }

    /// <summary>Pure mapping, unit-testable against a synthetic ContainerRow.</summary>
    internal static Container BuildContainerEntity(ContainerRow row, string runtime, ISecretRedactor secretRedactor)
    {
        var entity = new Container
        {
            Id = $"container:{runtime}:{row.ContainerId}",
            Name = row.Name ?? row.ContainerId,
            Type = "Container",
            Source = runtime,
            Status = string.Equals(row.State, "running", StringComparison.OrdinalIgnoreCase) ? EntityStatus.Running : EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            ImageTag = row.Image,
            Ports = row.Ports.Select(FormatPort).ToList(),
            Volumes = row.Mounts.Select(FormatMount).ToList(),
            Networks = row.NetworkNames,
            EnvironmentVariableNames = row.RawEnvironmentVariables.Select(ExtractEnvName).Where(n => n is not null).Select(n => n!).ToList(),
            Entrypoint = row.Entrypoint is not null ? secretRedactor.Redact(row.Entrypoint) : null,
            Command = row.Command is not null ? secretRedactor.Redact(row.Command) : null,
            RestartPolicy = row.RestartPolicy
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.DockerInspect, Location = $"{runtime} inspect", Detail = row.ContainerId });

        if (row.ImageId is not null) entity.SetMetadata("ImageId", row.ImageId);
        if (row.Created is not null) entity.SetMetadata("Created", row.Created.Value.ToString("O"));
        if (row.Status is not null) entity.SetMetadata("State", row.Status);
        if (row.Pid is not null) entity.SetMetadata("Pid", row.Pid.Value.ToString());

        for (var i = 0; i < row.Mounts.Count; i++)
        {
            var mount = row.Mounts[i];
            entity.SetMetadata($"Mount{i}.Type", mount.Type);
            if (mount.Source is not null) entity.SetMetadata($"Mount{i}.Source", mount.Source);
            entity.SetMetadata($"Mount{i}.Destination", mount.Destination);
            entity.SetMetadata($"Mount{i}.ReadOnly", mount.ReadOnly.ToString());
            if (mount.Propagation is not null) entity.SetMetadata($"Mount{i}.Propagation", mount.Propagation);
        }

        foreach (var (key, value) in row.RawLabels)
        {
            entity.SetMetadata($"Label.{key}", secretRedactor.Redact(value));
        }

        return entity;
    }

    internal static Container BuildImageEntity(ImageRow row, string runtime)
    {
        var tag = row.Repository is not null && row.Tag is not null ? $"{row.Repository}:{row.Tag}" : row.Repository ?? row.Tag;

        var entity = new Container
        {
            Id = $"image:{runtime}:{row.ImageId}",
            Name = tag ?? row.ImageId,
            Type = "Image",
            Source = runtime,
            Status = EntityStatus.Installed,
            Confidence = Confidence.VeryHigh(),
            ImageTag = tag
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.DockerInspect, Location = $"{runtime} images", Detail = row.ImageId });

        if (row.Created is not null) entity.SetMetadata("Created", row.Created.Value.ToString("O"));
        if (row.SizeDisplay is not null) entity.SetMetadata("SizeDisplay", row.SizeDisplay);

        return entity;
    }

    internal static Container BuildVolumeEntity(VolumeRow row, string runtime, ISecretRedactor secretRedactor)
    {
        var entity = new Container
        {
            Id = $"volume:{runtime}:{row.Name}",
            Name = row.Name,
            Type = "Volume",
            Source = runtime,
            Status = EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Path = row.Mountpoint
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.DockerInspect, Location = $"{runtime} volume ls", Detail = row.Name });

        if (row.Driver is not null) entity.SetMetadata("Driver", row.Driver);

        foreach (var (key, value) in row.RawLabels)
        {
            entity.SetMetadata($"Label.{key}", secretRedactor.Redact(value));
        }

        return entity;
    }

    internal static Container BuildNetworkEntity(NetworkRow row, string runtime)
    {
        var entity = new Container
        {
            Id = $"network:{runtime}:{row.NetworkId}",
            Name = row.Name,
            Type = "Network",
            Source = runtime,
            Status = EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Networks = [row.Name]
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.DockerInspect, Location = $"{runtime} network inspect", Detail = row.NetworkId });

        if (row.Driver is not null) entity.SetMetadata("Driver", row.Driver);
        if (row.Subnet is not null) entity.SetMetadata("Subnet", row.Subnet);
        if (row.Gateway is not null) entity.SetMetadata("Gateway", row.Gateway);
        if (row.AttachedContainerNames.Count > 0) entity.SetMetadata("AttachedContainers", string.Join(",", row.AttachedContainerNames));

        return entity;
    }

    private static string FormatPort(PortMappingRow port) =>
        port.HostPort is not null
            ? $"{port.HostAddress ?? "0.0.0.0"}:{port.HostPort}->{port.ContainerPort}/{port.Protocol}"
            : $"{port.ContainerPort}/{port.Protocol}";

    private static string FormatMount(MountRow mount) => $"{mount.Type}:{mount.Source ?? "?"}:{mount.Destination}";

    private static string? ExtractEnvName(string rawEnv)
    {
        var separatorIndex = rawEnv.IndexOf('=');
        return separatorIndex > 0 ? rawEnv[..separatorIndex] : null;
    }
}
