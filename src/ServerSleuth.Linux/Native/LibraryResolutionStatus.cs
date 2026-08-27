namespace ServerSleuth.Linux.Native;

/// <summary>Outcome of resolving one DT_NEEDED library name against bounded evidence — see
/// skill.md (Phase 6F) §10-11. A missing library is never fabricated; an ambiguous one is
/// never arbitrarily chosen.</summary>
public enum LibraryResolutionStatus
{
    Resolved,
    NotFound,
    Ambiguous,
    AccessDenied
}
