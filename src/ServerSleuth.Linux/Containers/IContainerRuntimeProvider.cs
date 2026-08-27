namespace ServerSleuth.Linux.Containers;

/// <summary>One provider per container runtime — never one scanner with a hard-coded if/else
/// per runtime. See skill.md (Phase 6C) §1.</summary>
public interface IContainerRuntimeProvider
{
    string RuntimeName { get; }

    Task<ContainerRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}
