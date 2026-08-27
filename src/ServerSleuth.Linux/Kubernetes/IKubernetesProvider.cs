namespace ServerSleuth.Linux.Kubernetes;

/// <summary>Kubernetes access via the currently configured kubectl context — see skill.md
/// (Phase 6D) §1. Kept as an interface (rather than a hard-coded call inside the scanner) so a
/// future non-kubectl acquisition path (e.g. a client library) could be added without touching
/// <see cref="LinuxKubernetesScanner"/>.</summary>
public interface IKubernetesProvider
{
    Task<KubernetesSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}
