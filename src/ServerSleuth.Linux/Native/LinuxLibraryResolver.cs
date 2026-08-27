using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Linux.Common;

namespace ServerSleuth.Linux.Native;

/// <summary>
/// Resolves a DT_NEEDED library name against explicit, bounded evidence in a fixed, documented
/// order — see skill.md (Phase 6F) §10:
///   1. Explicit RPATH entries (in file order — first existing match wins)
///   2. RUNPATH entries (in file order)
///   3. Already-discovered binary paths sharing the exact requested filename (ambiguous if more
///      than one distinct path matches — there is no principled way to prefer one)
///   4. A fixed list of well-known Linux library directories (in a documented order)
///   5. The optional ldconfig cache, if provided
/// Never searches a directory's contents — only ever checks for the existence of one exact
/// candidate path per tier/entry. Never resolves a path that would escape the filesystem root
/// (see <see cref="NativePathNormalizer"/>).
/// </summary>
public sealed class LinuxLibraryResolver(IFileSystemReader fileSystemReader) : ILibraryResolver
{
    private static readonly string[] WellKnownLibraryDirectories =
    [
        "/lib", "/lib64", "/usr/lib", "/usr/lib64",
        "/lib/x86_64-linux-gnu", "/usr/lib/x86_64-linux-gnu",
        "/lib/aarch64-linux-gnu", "/usr/lib/aarch64-linux-gnu"
    ];

    public LibraryResolutionResult Resolve(
        string libraryName,
        string? importingBinaryPath,
        IReadOnlyList<string> rpath,
        IReadOnlyList<string> runpath,
        IReadOnlyDictionary<string, IReadOnlyList<string>> knownBinaryPathsByFileName,
        IReadOnlyDictionary<string, string> ldconfigCache)
    {
        var importingDirectory = importingBinaryPath is not null ? LinuxPath.GetDirectoryName(importingBinaryPath) : null;
        var sawAccessDenied = false;

        var rpathResult = TryResolveFromDirectoryList(libraryName, rpath, importingDirectory, "RPATH", ref sawAccessDenied);
        if (rpathResult is not null)
        {
            return rpathResult;
        }

        var runpathResult = TryResolveFromDirectoryList(libraryName, runpath, importingDirectory, "RUNPATH", ref sawAccessDenied);
        if (runpathResult is not null)
        {
            return runpathResult;
        }

        if (knownBinaryPathsByFileName.TryGetValue(libraryName, out var knownPaths))
        {
            var distinctPaths = knownPaths.Distinct(StringComparer.Ordinal).ToList();
            if (distinctPaths.Count == 1)
            {
                return new LibraryResolutionResult { LibraryName = libraryName, Status = LibraryResolutionStatus.Resolved, ResolvedPath = distinctPaths[0], Source = "KnownBinary" };
            }

            if (distinctPaths.Count > 1)
            {
                return new LibraryResolutionResult { LibraryName = libraryName, Status = LibraryResolutionStatus.Ambiguous, Candidates = distinctPaths };
            }
        }

        var wellKnownResult = TryResolveFromDirectoryList(libraryName, WellKnownLibraryDirectories, importingBinaryDirectory: null, "WellKnownLocation", ref sawAccessDenied);
        if (wellKnownResult is not null)
        {
            return wellKnownResult;
        }

        if (ldconfigCache.TryGetValue(libraryName, out var ldconfigPath))
        {
            return new LibraryResolutionResult { LibraryName = libraryName, Status = LibraryResolutionStatus.Resolved, ResolvedPath = ldconfigPath, Source = "Ldconfig" };
        }

        return new LibraryResolutionResult
        {
            LibraryName = libraryName,
            Status = sawAccessDenied ? LibraryResolutionStatus.AccessDenied : LibraryResolutionStatus.NotFound
        };
    }

    private LibraryResolutionResult? TryResolveFromDirectoryList(
        string libraryName, IReadOnlyList<string> directories, string? importingBinaryDirectory, string source, ref bool sawAccessDenied)
    {
        foreach (var rawEntry in directories)
        {
            var expanded = NativePathNormalizer.ExpandOrigin(rawEntry, importingBinaryDirectory);
            var candidate = NativePathNormalizer.Normalize($"{expanded}/{libraryName}");

            var infoResult = fileSystemReader.GetFileInfo(candidate);
            if (infoResult.Status == OperationStatus.AccessDenied)
            {
                sawAccessDenied = true;
                continue;
            }

            if (infoResult.Success)
            {
                return new LibraryResolutionResult { LibraryName = libraryName, Status = LibraryResolutionStatus.Resolved, ResolvedPath = candidate, Source = source };
            }
        }

        return null;
    }
}
