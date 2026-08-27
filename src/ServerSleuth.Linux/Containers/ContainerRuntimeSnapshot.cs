namespace ServerSleuth.Linux.Containers;

public sealed record ContainerRuntimeSnapshot
{
    public required ContainerRuntimeAvailability Status { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<ContainerRow> Containers { get; init; } = [];
    public IReadOnlyList<ImageRow> Images { get; init; } = [];
    public IReadOnlyList<VolumeRow> Volumes { get; init; } = [];
    public IReadOnlyList<NetworkRow> Networks { get; init; } = [];
    public IReadOnlyList<string> PartialFailures { get; init; } = [];
}
