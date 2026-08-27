namespace ServerSleuth.Linux.Kubernetes;

/// <summary>One discovered PersistentVolume — see skill.md (Phase 6D) §17-18. When
/// <see cref="VolumeSourceType"/> is "HostPath", <see cref="HostPath"/> is only the declared
/// path string — it is never accessed, never recursively scanned, and symlinks are never
/// followed.</summary>
public sealed record PvRow
{
    public required string Name { get; init; }
    public string? Phase { get; init; }
    public string? Capacity { get; init; }
    public IReadOnlyList<string> AccessModes { get; init; } = [];
    public string? StorageClassName { get; init; }
    public string? ReclaimPolicy { get; init; }
    public string? VolumeSourceType { get; init; }
    public string? HostPath { get; init; }
}
