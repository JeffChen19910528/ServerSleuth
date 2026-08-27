namespace ServerSleuth.Core.Models;

/// <summary>A Kubernetes PersistentVolume — see skill.md (Phase 6D) §17-18. Never mounted, never
/// read. When the volume source is a hostPath, only the declared path string is recorded — it
/// is never accessed, never recursively scanned, and symlinks are never followed.</summary>
public sealed class KubernetesPersistentVolume : DiscoveryEntity
{
    public string? Phase { get; init; }
    public string? Capacity { get; init; }
    public IReadOnlyList<string> AccessModes { get; init; } = [];
    public string? StorageClassName { get; init; }
    public string? ReclaimPolicy { get; init; }
    public string? VolumeSourceType { get; init; } // "HostPath", "NFS", "AWSElasticBlockStore", etc.
    public string? HostPath { get; init; }
}
