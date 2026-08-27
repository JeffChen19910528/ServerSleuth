using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;

namespace ServerSleuth.Windows.Tests.Fakes;

internal sealed class FakeFileSystemReader : IFileSystemReader
{
    private readonly HashSet<string> _existingPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<string>> _directories = new(StringComparer.OrdinalIgnoreCase);

    public void AddExisting(params string[] paths)
    {
        foreach (var path in paths) _existingPaths.Add(path);
    }

    public void SetSubdirectories(string parent, params string[] subdirectories) =>
        _directories[parent] = subdirectories;

    public bool Exists(string path) => _existingPaths.Contains(path);

    public Task<FileSystemResult<string>> ReadTextAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(FileSystemResult<string>.Failure(OperationStatus.NotFound, "not implemented in fake"));

    public Task<FileSystemResult<byte[]>> ReadBytesAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(FileSystemResult<byte[]>.Failure(OperationStatus.NotFound, "not implemented in fake"));

    public FileSystemResult<FileEntryInfo> GetFileInfo(string path) =>
        _existingPaths.Contains(path)
            ? FileSystemResult<FileEntryInfo>.Ok(new FileEntryInfo { FullPath = path })
            : FileSystemResult<FileEntryInfo>.Failure(OperationStatus.NotFound, "not found");

    public FileSystemResult<IReadOnlyList<string>> EnumerateFiles(string directoryPath, string searchPattern = "*", bool recursive = false) =>
        FileSystemResult<IReadOnlyList<string>>.Ok([]);

    public FileSystemResult<IReadOnlyList<string>> EnumerateDirectories(string directoryPath, string searchPattern = "*", bool recursive = false) =>
        _directories.TryGetValue(directoryPath, out var subdirectories)
            ? FileSystemResult<IReadOnlyList<string>>.Ok(subdirectories)
            : FileSystemResult<IReadOnlyList<string>>.Failure(OperationStatus.NotFound, "not found");

    public FileSystemResult<string> ReadLinkTarget(string path) =>
        FileSystemResult<string>.Failure(OperationStatus.NotFound, "not implemented in fake");
}
