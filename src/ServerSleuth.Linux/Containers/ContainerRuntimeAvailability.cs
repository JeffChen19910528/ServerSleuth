namespace ServerSleuth.Linux.Containers;

/// <summary>
/// A container runtime's discoverability — see skill.md (Phase 6C) §2, §18. A runtime CLI
/// existing on PATH is never conflated with the runtime actually being queryable: `docker`
/// present but the daemon/socket unreachable is `Unavailable`, not `Supported`.
/// </summary>
public enum ContainerRuntimeAvailability
{
    Supported,
    PartiallySupported,
    NotInstalled,
    AccessDenied,
    Unavailable
}
