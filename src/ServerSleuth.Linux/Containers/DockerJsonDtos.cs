using System.Text.Json.Serialization;

namespace ServerSleuth.Linux.Containers;

/// <summary>
/// JSON shapes returned by `docker inspect`/`podman inspect` (container), `docker images
/// --format {{json .}}`, `docker volume ls --format {{json .}}`, and `docker network inspect`.
/// Podman's CLI is designed to be output-compatible with Docker's for these read-only
/// subcommands, so one DTO set covers both providers — see ARCHITECTURE.md's Phase 6C
/// addendum for the disclosed limitation if a given Podman version genuinely diverges.
/// </summary>
internal sealed record DockerInspectContainer
{
    [JsonPropertyName("Id")] public string? Id { get; init; }
    [JsonPropertyName("Created")] public string? Created { get; init; }
    [JsonPropertyName("Name")] public string? Name { get; init; }
    [JsonPropertyName("Image")] public string? ImageId { get; init; }
    [JsonPropertyName("State")] public DockerInspectState? State { get; init; }
    [JsonPropertyName("Config")] public DockerInspectConfig? Config { get; init; }
    [JsonPropertyName("HostConfig")] public DockerInspectHostConfig? HostConfig { get; init; }
    [JsonPropertyName("Mounts")] public List<DockerInspectMount>? Mounts { get; init; }
    [JsonPropertyName("NetworkSettings")] public DockerInspectNetworkSettings? NetworkSettings { get; init; }
}

internal sealed record DockerInspectState
{
    [JsonPropertyName("Status")] public string? Status { get; init; }
    [JsonPropertyName("Pid")] public int? Pid { get; init; }
}

internal sealed record DockerInspectConfig
{
    [JsonPropertyName("Image")] public string? Image { get; init; }
    [JsonPropertyName("Entrypoint")] public List<string>? Entrypoint { get; init; }
    [JsonPropertyName("Cmd")] public List<string>? Cmd { get; init; }
    [JsonPropertyName("Env")] public List<string>? Env { get; init; }
    [JsonPropertyName("Labels")] public Dictionary<string, string>? Labels { get; init; }
}

internal sealed record DockerInspectHostConfig
{
    [JsonPropertyName("RestartPolicy")] public DockerInspectRestartPolicy? RestartPolicy { get; init; }
}

internal sealed record DockerInspectRestartPolicy
{
    [JsonPropertyName("Name")] public string? Name { get; init; }
}

internal sealed record DockerInspectMount
{
    [JsonPropertyName("Type")] public string? Type { get; init; }
    [JsonPropertyName("Source")] public string? Source { get; init; }
    [JsonPropertyName("Destination")] public string? Destination { get; init; }
    [JsonPropertyName("RW")] public bool? ReadWrite { get; init; }
    [JsonPropertyName("Propagation")] public string? Propagation { get; init; }
}

internal sealed record DockerInspectNetworkSettings
{
    [JsonPropertyName("Networks")] public Dictionary<string, object>? Networks { get; init; }
    [JsonPropertyName("Ports")] public Dictionary<string, List<DockerInspectPortBinding>?>? Ports { get; init; }
}

internal sealed record DockerInspectPortBinding
{
    [JsonPropertyName("HostIp")] public string? HostIp { get; init; }
    [JsonPropertyName("HostPort")] public string? HostPort { get; init; }
}

internal sealed record DockerImageListEntry
{
    [JsonPropertyName("ID")] public string? Id { get; init; }
    [JsonPropertyName("Repository")] public string? Repository { get; init; }
    [JsonPropertyName("Tag")] public string? Tag { get; init; }
    [JsonPropertyName("CreatedAt")] public string? CreatedAt { get; init; }
    [JsonPropertyName("Size")] public string? Size { get; init; }
}

internal sealed record DockerVolumeListEntry
{
    [JsonPropertyName("Name")] public string? Name { get; init; }
    [JsonPropertyName("Driver")] public string? Driver { get; init; }
    [JsonPropertyName("Mountpoint")] public string? Mountpoint { get; init; }
    [JsonPropertyName("Labels")] public string? Labels { get; init; } // Docker emits this as a "k=v,k2=v2" string, not an object
}

internal sealed record DockerInspectNetwork
{
    [JsonPropertyName("Id")] public string? Id { get; init; }
    [JsonPropertyName("Name")] public string? Name { get; init; }
    [JsonPropertyName("Driver")] public string? Driver { get; init; }
    [JsonPropertyName("IPAM")] public DockerInspectIpam? Ipam { get; init; }
    [JsonPropertyName("Containers")] public Dictionary<string, DockerInspectNetworkContainer>? Containers { get; init; }
}

internal sealed record DockerInspectIpam
{
    [JsonPropertyName("Config")] public List<DockerInspectIpamConfig>? Config { get; init; }
}

internal sealed record DockerInspectIpamConfig
{
    [JsonPropertyName("Subnet")] public string? Subnet { get; init; }
    [JsonPropertyName("Gateway")] public string? Gateway { get; init; }
}

internal sealed record DockerInspectNetworkContainer
{
    [JsonPropertyName("Name")] public string? Name { get; init; }
}
