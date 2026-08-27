namespace ServerSleuth.Linux.Native;

/// <summary>Resolves one DT_NEEDED library name against explicit, bounded evidence only — never
/// a filesystem-wide search. Kept separate from ELF parsing (<see cref="ILinuxElfParser"/>) and
/// entity creation. See skill.md (Phase 6F) §10-11.</summary>
public interface ILibraryResolver
{
    LibraryResolutionResult Resolve(
        string libraryName,
        string? importingBinaryPath,
        IReadOnlyList<string> rpath,
        IReadOnlyList<string> runpath,
        IReadOnlyDictionary<string, IReadOnlyList<string>> knownBinaryPathsByFileName,
        IReadOnlyDictionary<string, string> ldconfigCache);
}
