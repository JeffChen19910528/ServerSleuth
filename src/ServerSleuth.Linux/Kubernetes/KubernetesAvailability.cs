namespace ServerSleuth.Linux.Kubernetes;

/// <summary>Kubernetes access's discoverability — see skill.md (Phase 6D) §2. `kubectl` existing
/// on PATH is never conflated with a cluster actually being reachable: successful discovery
/// requires a successful read-only API query (`kubectl version -o json`), not merely a present
/// executable.</summary>
public enum KubernetesAvailability
{
    Supported,
    PartiallySupported,
    NotInstalled,
    AccessDenied,
    Unavailable
}
