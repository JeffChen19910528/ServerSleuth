namespace ServerSleuth.Core.Models;

/// <summary>A Kubernetes pod — see skill.md (Phase 6D) §9. PodIP/HostIP are recorded as facts
/// only; the scanner never connects to or probes either address.</summary>
public sealed class KubernetesPod : DiscoveryEntity
{
    public string? Namespace { get; init; }
    public string? Uid { get; init; }
    public string? Phase { get; init; } // "Running", "Pending", "Succeeded", "Failed", "Unknown"
    public string? NodeName { get; init; }
    public string? PodIp { get; init; }
    public string? HostIp { get; init; }
}
