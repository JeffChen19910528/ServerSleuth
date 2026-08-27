using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;

namespace ServerSleuth.Windows.Binaries;

/// <summary>
/// A depth- and file-count-bounded, non-reparse-point-following directory walk — the
/// alternative to IFileSystemReader.EnumerateFiles's unbounded recursive=true for this
/// scanner's needs. Never follows a junction/symlink/reparse point, preventing cycles; a
/// depth or file-count limit is recorded, never silently ignored. See skill.md §20-22.
/// </summary>
public static class BoundedDirectoryWalker
{
    public static DirectoryWalkResult Walk(
        IFileSystemReader fileSystemReader,
        string rootPath,
        IReadOnlyList<string> searchPatterns,
        int maxDepth = BinaryDiscoveryDefaults.MaxDirectoryDepth,
        int maxFiles = BinaryDiscoveryDefaults.MaxFilesPerRoot)
    {
        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var depthLimitReached = false;
        var fileLimitReached = false;
        var reparsePointsSkipped = 0;
        var accessDeniedDirectories = new List<string>();

        void Recurse(string directory, int depth)
        {
            if (fileLimitReached)
            {
                return;
            }

            if (depth > maxDepth)
            {
                depthLimitReached = true;
                return;
            }

            foreach (var pattern in searchPatterns)
            {
                var filesResult = fileSystemReader.EnumerateFiles(directory, pattern, recursive: false);
                if (!filesResult.Success)
                {
                    if (filesResult.Status == OperationStatus.AccessDenied)
                    {
                        accessDeniedDirectories.Add(directory);
                    }
                    continue;
                }

                foreach (var file in filesResult.Value!)
                {
                    if (!seen.Add(file))
                    {
                        continue;
                    }

                    if (files.Count >= maxFiles)
                    {
                        fileLimitReached = true;
                        return;
                    }

                    files.Add(file);
                }
            }

            var directoriesResult = fileSystemReader.EnumerateDirectories(directory);
            if (!directoriesResult.Success)
            {
                return;
            }

            foreach (var subDirectory in directoriesResult.Value!)
            {
                if (fileLimitReached)
                {
                    return;
                }

                var info = fileSystemReader.GetFileInfo(subDirectory);
                if (info.Success && info.Value!.IsReparsePoint)
                {
                    reparsePointsSkipped++;
                    continue; // never follow — prevents cycles, see skill.md §20
                }

                Recurse(subDirectory, depth + 1);
            }
        }

        Recurse(rootPath, 0);

        return new DirectoryWalkResult
        {
            Files = files,
            DepthLimitReached = depthLimitReached,
            FileLimitReached = fileLimitReached,
            ReparsePointsSkipped = reparsePointsSkipped,
            AccessDeniedDirectories = accessDeniedDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }
}
