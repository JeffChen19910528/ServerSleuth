using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;

namespace ServerSleuth.Linux.Tests.Fixtures;

/// <summary>Deterministic in-memory fake of IFileSystemReader — no real filesystem access,
/// since the real paths (/proc, /etc/os-release) don't exist on the Windows dev machine.</summary>
public sealed class FakeFileSystemReader : IFileSystemReader
{
    private readonly Dictionary<string, string> _textFiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OperationStatus> _textFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _accessDeniedDirectories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FileEntryInfo> _fileInfos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OperationStatus> _fileInfoFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _byteFiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OperationStatus> _byteFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _linkTargets = new(StringComparer.Ordinal);

    public void SetText(string path, string content) => _textFiles[path] = content;

    public void SetBytes(string path, byte[] content) => _byteFiles[path] = content;

    public void SetBytesFailure(string path, OperationStatus status) => _byteFailures[path] = status;

    public void SetTextFailure(string path, OperationStatus status) => _textFailures[path] = status;

    public void SetDirectoryEntries(string directory, params string[] entries) => _directories[directory] = [.. entries];

    public void SetFileEntries(string directory, params string[] entries) => _files[directory] = [.. entries];

    public void SetDirectoryAccessDenied(string directory) => _accessDeniedDirectories.Add(directory);

    public void SetFileInfo(string path, long sizeBytes = 100, DateTimeOffset? lastWriteTimeUtc = null, bool isReparsePoint = false) =>
        _fileInfos[path] = new FileEntryInfo
        {
            FullPath = path,
            SizeBytes = sizeBytes,
            LastWriteTimeUtc = lastWriteTimeUtc ?? DateTimeOffset.UtcNow,
            IsReparsePoint = isReparsePoint
        };

    public void SetFileInfoFailure(string path, OperationStatus status) => _fileInfoFailures[path] = status;

    public void SetLinkTarget(string path, string target) => _linkTargets[path] = target;

    public bool Exists(string path) => _textFiles.ContainsKey(path);

    public FileSystemResult<string> ReadLinkTarget(string path) =>
        _linkTargets.TryGetValue(path, out var target)
            ? FileSystemResult<string>.Ok(target)
            : FileSystemResult<string>.Failure(OperationStatus.NotFound, "not a symlink");

    public Task<FileSystemResult<string>> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        if (_textFailures.TryGetValue(path, out var status))
        {
            return Task.FromResult(FileSystemResult<string>.Failure(status, $"{status}"));
        }

        return Task.FromResult(_textFiles.TryGetValue(path, out var content)
            ? FileSystemResult<string>.Ok(content)
            : FileSystemResult<string>.Failure(OperationStatus.NotFound, "not found"));
    }

    public Task<FileSystemResult<byte[]>> ReadBytesAsync(string path, CancellationToken cancellationToken)
    {
        if (_byteFailures.TryGetValue(path, out var status))
        {
            return Task.FromResult(FileSystemResult<byte[]>.Failure(status, $"{status}"));
        }

        return Task.FromResult(_byteFiles.TryGetValue(path, out var content)
            ? FileSystemResult<byte[]>.Ok(content)
            : FileSystemResult<byte[]>.Failure(OperationStatus.NotFound, "not found"));
    }

    public FileSystemResult<FileEntryInfo> GetFileInfo(string path)
    {
        if (_fileInfoFailures.TryGetValue(path, out var status))
        {
            return FileSystemResult<FileEntryInfo>.Failure(status, $"{status}");
        }

        return _fileInfos.TryGetValue(path, out var info)
            ? FileSystemResult<FileEntryInfo>.Ok(info)
            : FileSystemResult<FileEntryInfo>.Failure(OperationStatus.NotFound, "not found");
    }

    public FileSystemResult<IReadOnlyList<string>> EnumerateFiles(string directoryPath, string searchPattern = "*", bool recursive = false)
    {
        if (_accessDeniedDirectories.Contains(directoryPath))
        {
            return FileSystemResult<IReadOnlyList<string>>.Failure(OperationStatus.AccessDenied, "access denied");
        }

        return _files.TryGetValue(directoryPath, out var entries)
            ? FileSystemResult<IReadOnlyList<string>>.Ok(entries)
            : FileSystemResult<IReadOnlyList<string>>.Failure(OperationStatus.NotFound, "not found");
    }

    public FileSystemResult<IReadOnlyList<string>> EnumerateDirectories(string directoryPath, string searchPattern = "*", bool recursive = false)
    {
        if (_accessDeniedDirectories.Contains(directoryPath))
        {
            return FileSystemResult<IReadOnlyList<string>>.Failure(OperationStatus.AccessDenied, "access denied");
        }

        return _directories.TryGetValue(directoryPath, out var entries)
            ? FileSystemResult<IReadOnlyList<string>>.Ok(entries)
            : FileSystemResult<IReadOnlyList<string>>.Failure(OperationStatus.NotFound, "not found");
    }
}
