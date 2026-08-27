namespace ServerSleuth.Core.Models;

/// <summary>A Kubernetes PersistentVolumeClaim — see skill.md (Phase 6D) §16. Never mounted,
/// never read.</summary>
public sealed class KubernetesPersistentVolumeClaim : DiscoveryEntity
{
    public string? Namespace { get; init; }
    public string? Phase { get; init; } // "Bound", "Pending", "Lost"
    public string? Capacity { get; init; }
    public IReadOnlyList<string> AccessModes { get; init; } = [];
    public string? StorageClassName { get; init; }
    public string? VolumeName { get; init; }
    public string? VolumeMode { get; init; }
}
