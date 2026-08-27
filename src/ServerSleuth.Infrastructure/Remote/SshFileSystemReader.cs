using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;

namespace ServerSleuth.Infrastructure.Remote;

/// <summary>
/// <see cref="IFileSystemReader"/> over SFTP — see skill.md (Phase 10D-2) §10. Every member maps
/// onto a structured SFTP primitive via <see cref="ISshSession"/>, never a shell command (no
/// <c>cat</c>/<c>ls</c>/<c>find</c> invocation anywhere here) — preferring SFTP over "run a shell
/// command and parse its output" is both safer (no quoting/escaping surface at all) and exactly
/// what skill.md §10 asks for. <see cref="EnumerateFiles"/>/<see cref="EnumerateDirectories"/>
/// are always non-recursive at the SFTP layer (one directory listing), matching the existing
/// bounded-scan discipline every Linux scanner already follows locally — recursion, where a
/// scanner needs it, stays the SCANNER's own bounded walk (skill.md §10, §18: "never
/// recursively walk the entire remote filesystem").
/// </summary>
public sealed class SshFileSystemReader(ISshSession session) : IFileSystemReader
{
    public bool Exists(string path) => session.SftpExists(path);

    public Task<FileSystemResult<string>> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        var bytesResult = session.SftpReadBytes(path);
        if (!bytesResult.Success)
        {
            return Task.FromResult(FileSystemResult<string>.Failure(bytesResult.Status, bytesResult.ErrorMessage ?? string.Empty));
        }

        return Task.FromResult(FileSystemResult<string>.Ok(System.Text.Encoding.UTF8.GetString(bytesResult.Value!)));
    }

    public Task<FileSystemResult<byte[]>> ReadBytesAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(session.SftpReadBytes(path));

    public FileSystemResult<FileEntryInfo> GetFileInfo(string path)
    {
        var attributes = session.SftpGetAttributes(path);
        if (!attributes.Success)
        {
            return FileSystemResult<FileEntryInfo>.Failure(attributes.Status, attributes.ErrorMessage ?? string.Empty);
        }

        return FileSystemResult<FileEntryInfo>.Ok(ToFileEntryInfo(attributes.Value!));
    }

    public FileSystemResult<IReadOnlyList<string>> EnumerateFiles(string directoryPath, string searchPattern = "*", bool recursive = false)
    {
        if (recursive)
        {
            // Phase 10D-2 §10, §18: never a remote recursive filesystem walk — a scanner needing
            // recursion must do its own bounded, bottom-up traversal one directory at a time.
            return FileSystemResult<IReadOnlyList<string>>.Failure(
                OperationStatus.InvalidInput, "Recursive remote enumeration is not supported — enumerate one directory at a time.");
        }

        var listing = session.SftpListDirectory(directoryPath);
        if (!listing.Success)
        {
            return FileSystemResult<IReadOnlyList<string>>.Failure(listing.Status, listing.ErrorMessage ?? string.Empty);
        }

        IReadOnlyList<string> files = listing.Value!
            .Where(e => !e.IsDirectory && MatchesPattern(e.FullPath, searchPattern))
            .Select(e => e.FullPath)
            .ToList();

        return FileSystemResult<IReadOnlyList<string>>.Ok(files);
    }

    public FileSystemResult<IReadOnlyList<string>> EnumerateDirectories(string directoryPath, string searchPattern = "*", bool recursive = false)
    {
        if (recursive)
        {
            return FileSystemResult<IReadOnlyList<string>>.Failure(
                OperationStatus.InvalidInput, "Recursive remote enumeration is not supported — enumerate one directory at a time.");
        }

        var listing = session.SftpListDirectory(directoryPath);
        if (!listing.Success)
        {
            return FileSystemResult<IReadOnlyList<string>>.Failure(listing.Status, listing.ErrorMessage ?? string.Empty);
        }

        IReadOnlyList<string> directories = listing.Value!
            .Where(e => e.IsDirectory && MatchesPattern(e.FullPath, searchPattern))
            .Select(e => e.FullPath)
            .ToList();

        return FileSystemResult<IReadOnlyList<string>>.Ok(directories);
    }

    public FileSystemResult<string> ReadLinkTarget(string path) => session.ReadLinkTarget(path);

    private static bool MatchesPattern(string fullPath, string searchPattern) =>
        searchPattern == "*" || System.IO.Path.GetFileName(fullPath.TrimEnd('/')).Contains(searchPattern.Trim('*'), StringComparison.OrdinalIgnoreCase);

    private static FileEntryInfo ToFileEntryInfo(SshRemoteFileInfo remote) => new()
    {
        FullPath = remote.FullPath,
        SizeBytes = remote.SizeBytes,
        LastWriteTimeUtc = remote.LastWriteTimeUtc,
        IsDirectory = remote.IsDirectory,
        IsReparsePoint = remote.IsSymbolicLink
    };
}
