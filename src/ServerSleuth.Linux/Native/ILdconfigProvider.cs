namespace ServerSleuth.Linux.Native;

/// <summary>Optional last-resort resolution tier backed by `ldconfig -p` (the loader cache
/// listing) — see skill.md (Phase 6F) §12. Never invokes `ldconfig` without `-p`, never modifies
/// the loader cache. Absence of this provider (or a failed query) simply means this tier
/// contributes nothing — it never fails the whole scan.</summary>
public interface ILdconfigProvider
{
    /// <summary>Returns the cache as name → resolved path, or an empty dictionary if
    /// `ldconfig` is unavailable or its output could not be parsed.</summary>
    Task<IReadOnlyDictionary<string, string>> GetCacheAsync(CancellationToken cancellationToken);
}
