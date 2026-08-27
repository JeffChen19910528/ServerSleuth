using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Linux.Containers;

public sealed class DockerContainerRuntimeProvider(IProcessRunner processRunner) : IContainerRuntimeProvider
{
    private readonly ContainerCliRuntimeProvider _inner = new("docker", processRunner);

    public string RuntimeName => "docker";

    public Task<ContainerRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) =>
        _inner.GetSnapshotAsync(cancellationToken);
}
