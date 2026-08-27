using System.Text.Json;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Linux.Containers;

/// <summary>
/// Shared, read-only CLI-based discovery for any Docker-CLI-compatible container runtime.
/// Docker and Podman are output-compatible for every subcommand used here (`ps`, `inspect`,
/// `images`, `volume ls`, `network ls`/`inspect`, all with `--format json`/`{{json .}}`), so
/// this one implementation backs both providers rather than duplicating the parsing logic —
/// see skill.md (Phase 6C) §1. Only read-only subcommands are ever invoked; never `run`/`exec`/
/// `start`/`stop`/`rm`/`pull`/`push`/`build`/`commit`, and never a shell.
/// </summary>
internal sealed class ContainerCliRuntimeProvider(string executable, IProcessRunner processRunner)
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    public async Task<ContainerRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var psResult = await Run(["ps", "-aq", "--no-trunc"], cancellationToken);

        if (!psResult.Success)
        {
            return Classify(psResult);
        }

        var partialFailures = new List<string>();

        var containers = await ReadContainers(psResult.StandardOutput, partialFailures, cancellationToken);
        var images = await ReadImages(partialFailures, cancellationToken);
        var volumes = await ReadVolumes(partialFailures, cancellationToken);
        var networks = await ReadNetworks(partialFailures, cancellationToken);

        var status = partialFailures.Count > 0 ? ContainerRuntimeAvailability.PartiallySupported : ContainerRuntimeAvailability.Supported;

        return new ContainerRuntimeSnapshot
        {
            Status = status,
            Containers = containers,
            Images = images,
            Volumes = volumes,
            Networks = networks,
            PartialFailures = partialFailures
        };
    }

    private async Task<List<ContainerRow>> ReadContainers(string psOutput, List<string> partialFailures, CancellationToken cancellationToken)
    {
        var ids = SplitLines(psOutput);
        if (ids.Count == 0)
        {
            return [];
        }

        var inspectResult = await Run(["inspect", .. ids], cancellationToken);
        if (!inspectResult.Success)
        {
            partialFailures.Add($"container inspect: {inspectResult.Status}");
            return [];
        }

        var dtos = TryDeserialize<List<DockerInspectContainer>>(inspectResult.StandardOutput) ?? [];
        return dtos.Select(MapContainer).ToList();
    }

    private async Task<List<ImageRow>> ReadImages(List<string> partialFailures, CancellationToken cancellationToken)
    {
        var result = await Run(["images", "--no-trunc", "--format", "{{json .}}"], cancellationToken);
        if (!result.Success)
        {
            partialFailures.Add($"images: {result.Status}");
            return [];
        }

        var images = new List<ImageRow>();
        foreach (var line in SplitLines(result.StandardOutput))
        {
            var entry = TryDeserialize<DockerImageListEntry>(line);
            if (entry?.Id is null)
            {
                continue; // malformed line — skipped, never guessed at
            }

            images.Add(new ImageRow
            {
                ImageId = entry.Id,
                Repository = entry.Repository,
                Tag = entry.Tag,
                Created = TryParseTimestamp(entry.CreatedAt),
                SizeDisplay = entry.Size
            });
        }

        return images;
    }

    private async Task<List<VolumeRow>> ReadVolumes(List<string> partialFailures, CancellationToken cancellationToken)
    {
        var result = await Run(["volume", "ls", "--format", "{{json .}}"], cancellationToken);
        if (!result.Success)
        {
            partialFailures.Add($"volume ls: {result.Status}");
            return [];
        }

        var volumes = new List<VolumeRow>();
        foreach (var line in SplitLines(result.StandardOutput))
        {
            var entry = TryDeserialize<DockerVolumeListEntry>(line);
            if (entry?.Name is null)
            {
                continue; // malformed line — skipped, never guessed at
            }

            volumes.Add(new VolumeRow
            {
                Name = entry.Name,
                Driver = entry.Driver,
                Mountpoint = entry.Mountpoint,
                RawLabels = ParseLabelString(entry.Labels)
            });
        }

        return volumes;
    }

    private async Task<List<NetworkRow>> ReadNetworks(List<string> partialFailures, CancellationToken cancellationToken)
    {
        var listResult = await Run(["network", "ls", "-q", "--no-trunc"], cancellationToken);
        if (!listResult.Success)
        {
            partialFailures.Add($"network ls: {listResult.Status}");
            return [];
        }

        var ids = SplitLines(listResult.StandardOutput);
        if (ids.Count == 0)
        {
            return [];
        }

        var inspectResult = await Run(["network", "inspect", .. ids], cancellationToken);
        if (!inspectResult.Success)
        {
            partialFailures.Add($"network inspect: {inspectResult.Status}");
            return [];
        }

        var dtos = TryDeserialize<List<DockerInspectNetwork>>(inspectResult.StandardOutput) ?? [];
        return dtos.Select(MapNetwork).ToList();
    }

    private static ContainerRow MapContainer(DockerInspectContainer dto)
    {
        var ports = new List<PortMappingRow>();
        if (dto.NetworkSettings?.Ports is not null)
        {
            foreach (var (key, bindings) in dto.NetworkSettings.Ports)
            {
                var parts = key.Split('/');
                if (parts.Length != 2 || !int.TryParse(parts[0], out var containerPort))
                {
                    continue;
                }

                var protocol = parts[1];

                if (bindings is null || bindings.Count == 0)
                {
                    ports.Add(new PortMappingRow { ContainerPort = containerPort, Protocol = protocol });
                    continue;
                }

                foreach (var binding in bindings)
                {
                    ports.Add(new PortMappingRow
                    {
                        HostAddress = binding.HostIp,
                        HostPort = int.TryParse(binding.HostPort, out var hostPort) ? hostPort : null,
                        ContainerPort = containerPort,
                        Protocol = protocol
                    });
                }
            }
        }

        var mounts = dto.Mounts?.Select(m => new MountRow
        {
            Type = m.Type ?? "unknown",
            Source = m.Source,
            Destination = m.Destination ?? string.Empty,
            ReadOnly = m.ReadWrite == false,
            Propagation = m.Propagation
        }).ToList() ?? [];

        return new ContainerRow
        {
            ContainerId = dto.Id ?? "unknown",
            Name = dto.Name?.TrimStart('/'),
            Image = dto.Config?.Image ?? dto.ImageId,
            ImageId = dto.ImageId,
            Created = TryParseTimestamp(dto.Created),
            State = dto.State?.Status,
            Status = dto.State?.Status,
            RestartPolicy = dto.HostConfig?.RestartPolicy?.Name,
            Entrypoint = dto.Config?.Entrypoint is { Count: > 0 } ep ? string.Join(' ', ep) : null,
            Command = dto.Config?.Cmd is { Count: > 0 } cmd ? string.Join(' ', cmd) : null,
            Pid = dto.State?.Pid,
            RawEnvironmentVariables = dto.Config?.Env ?? [],
            RawLabels = dto.Config?.Labels ?? new Dictionary<string, string>(),
            Ports = ports,
            Mounts = mounts,
            NetworkNames = dto.NetworkSettings?.Networks?.Keys.ToList() ?? []
        };
    }

    private static NetworkRow MapNetwork(DockerInspectNetwork dto) => new()
    {
        NetworkId = dto.Id ?? "unknown",
        Name = dto.Name ?? "unknown",
        Driver = dto.Driver,
        Subnet = dto.Ipam?.Config?.FirstOrDefault()?.Subnet,
        Gateway = dto.Ipam?.Config?.FirstOrDefault()?.Gateway,
        AttachedContainerNames = dto.Containers?.Values.Select(c => c.Name ?? "unknown").ToList() ?? []
    };

    private static Dictionary<string, string> ParseLabelString(string? labels)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(labels))
        {
            return result;
        }

        foreach (var pair in labels.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            result[pair[..separatorIndex]] = pair[(separatorIndex + 1)..];
        }

        return result;
    }

    private static ContainerRuntimeSnapshot Classify(ProcessResult result)
    {
        // A missing executable can surface as either OperationStatus.StartFailed or the more
        // specific OperationStatus.NotFound (ProcessRunner's classification for the common "no
        // such file" case) — see the identical fix and rationale in
        // KubectlKubernetesProvider.Classify, found via Phase 6G's real Linux execution.
        if (result.Status is OperationStatus.StartFailed or OperationStatus.NotFound)
        {
            return new ContainerRuntimeSnapshot { Status = ContainerRuntimeAvailability.NotInstalled };
        }

        var stderr = result.StandardError;
        var status = stderr.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
            ? ContainerRuntimeAvailability.AccessDenied
            : ContainerRuntimeAvailability.Unavailable;

        return new ContainerRuntimeSnapshot { Status = status, ErrorMessage = stderr.Length > 0 ? stderr : result.Status.ToString() };
    }

    private Task<ProcessResult> Run(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        processRunner.RunAsync(new ProcessRequest { Executable = executable, Arguments = arguments, Timeout = CommandTimeout }, cancellationToken);

    private static List<string> SplitLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static T? TryDeserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return null; // malformed output — skipped, never guessed at
        }
    }

    private static DateTimeOffset? TryParseTimestamp(string? value) =>
        value is not null && DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
}
