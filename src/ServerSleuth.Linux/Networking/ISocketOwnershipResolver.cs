namespace ServerSleuth.Linux.Networking;

/// <summary>Resolves a socket's kernel inode number to the owning process id, via
/// `/proc/&lt;pid&gt;/fd/*` symlink targets shaped `socket:[inode]` — see skill.md (Phase 6A) §6.
/// Never guesses: a pid whose `fd` directory can't be read (not our own process, not root)
/// simply contributes nothing to the map, exactly as an inode with no matching entry.</summary>
public interface ISocketOwnershipResolver
{
    IReadOnlyDictionary<string, int> BuildInodeToPidMap();
}
