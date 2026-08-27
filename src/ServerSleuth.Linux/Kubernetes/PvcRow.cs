namespace ServerSleuth.Linux.Kubernetes;

/// <summary>One discovered PersistentVolumeClaim — see skill.md (Phase 6D) §16. Never mounted,
/// never read.</summary>
public sealed record PvcRow
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public string? Phase { get; init; }
    public string? Capacity { get; init; }
    public IReadOnlyList<string> AccessModes { get; init; } = [];
    public string? StorageClassName { get; init; }
    public string? VolumeName { get; init; }
    public string? VolumeMode { get; init; }
}
