namespace ServerSleuth.Infrastructure.FileSystem;

/// <summary>
/// Read-only filesystem access used by scanners instead of raw File/Directory calls, so
/// AccessDenied/NotFound/IOError are handled uniformly and a single bad path can never crash
/// a scan. See skill.md §36 (do not scan the entire filesystem indiscriminately) — callers
/// are expected to pass targeted paths, not walk an entire drive.
/// </summary>
public interface IFileSystemReader
{
    bool Exists(string path);

    Task<FileSystemResult<string>> ReadTextAsync(string path, CancellationToken cancellationToken);

    Task<FileSystemResult<byte[]>> ReadBytesAsync(string path, CancellationToken cancellationToken);

    FileSystemResult<FileEntryInfo> GetFileInfo(string path);

    FileSystemResult<IReadOnlyList<string>> EnumerateFiles(string directoryPath, string searchPattern = "*", bool recursive = false);

    FileSystemResult<IReadOnlyList<string>> EnumerateDirectories(string directoryPath, string searchPattern = "*", bool recursive = false);

    /// <summary>
    /// The target of a symbolic link at <paramref name="path"/> — added in Phase 10D-2 to close
    /// a genuine interface gap: <c>LinuxProcProvider</c> needed to resolve
    /// <c>/proc/&lt;pid&gt;/exe</c> (skill.md §11) but had no way to do so through this interface,
    /// and instead called <c>FileInfo.LinkTarget</c> directly on the LOCAL filesystem — silently
    /// wrong for a remote target. A non-symlink path, or one that cannot be read, returns a
    /// non-success <see cref="OperationStatus"/> (typically <see cref="OperationStatus.NotFound"/>),
    /// never an exception.
    /// </summary>
    FileSystemResult<string> ReadLinkTarget(string path);
}
