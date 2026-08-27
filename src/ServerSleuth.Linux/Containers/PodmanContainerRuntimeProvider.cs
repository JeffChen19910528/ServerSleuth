using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Linux.Containers;

/// <summary>Supports both rootful and rootless Podman — this provider never assumes a
/// system-wide daemon exists (rootless Podman has none); a rootless `podman` on PATH is queried
/// exactly the same way as rootful, since the CLI itself abstracts that distinction.</summary>
public sealed class PodmanContainerRuntimeProvider(IProcessRunner processRunner) : IContainerRuntimeProvider
{
    private readonly ContainerCliRuntimeProvider _inner = new("podman", processRunner);

    public string RuntimeName => "podman";

    public Task<ContainerRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) =>
        _inner.GetSnapshotAsync(cancellationToken);
}
