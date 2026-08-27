using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Infrastructure.FileSystem;

public sealed class FileSystemReader : IFileSystemReader
{
    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    public async Task<FileSystemResult<string>> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken);
            return FileSystemResult<string>.Ok(text);
        }
        catch (Exception ex) when (TryClassify(ex, out var status))
        {
            return FileSystemResult<string>.Failure(status, ex.Message);
        }
    }

    public async Task<FileSystemResult<byte[]>> ReadBytesAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            return FileSystemResult<byte[]>.Ok(bytes);
        }
        catch (Exception ex) when (TryClassify(ex, out var status))
        {
            return FileSystemResult<byte[]>.Failure(status, ex.Message);
        }
    }

    public FileSystemResult<FileEntryInfo> GetFileInfo(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var dirInfo = new DirectoryInfo(path);
                return FileSystemResult<FileEntryInfo>.Ok(new FileEntryInfo
                {
                    FullPath = dirInfo.FullName,
                    LastWriteTimeUtc = dirInfo.LastWriteTimeUtc,
                    IsDirectory = true,
                    IsReparsePoint = dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)
                });
            }

            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                return FileSystemResult<FileEntryInfo>.Failure(OperationStatus.NotFound, $"Path not found: {path}");
            }

            return FileSystemResult<FileEntryInfo>.Ok(new FileEntryInfo
            {
                FullPath = fileInfo.FullName,
                SizeBytes = fileInfo.Length,
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
                IsDirectory = false,
                IsReadOnly = fileInfo.IsReadOnly,
                IsReparsePoint = fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)
            });
        }
        catch (Exception ex) when (TryClassify(ex, out var status))
        {
            return FileSystemResult<FileEntryInfo>.Failure(status, ex.Message);
        }
    }

    public FileSystemResult<IReadOnlyList<string>> EnumerateFiles(string directoryPath, string searchPattern = "*", bool recursive = false)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                return FileSystemResult<IReadOnlyList<string>>.Failure(OperationStatus.NotFound, $"Directory not found: {directoryPath}");
            }

            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            IReadOnlyList<string> files = Directory.EnumerateFiles(directoryPath, searchPattern, option).ToList();
            return FileSystemResult<IReadOnlyList<string>>.Ok(files);
        }
        catch (Exception ex) when (TryClassify(ex, out var status))
        {
            return FileSystemResult<IReadOnlyList<string>>.Failure(status, ex.Message);
        }
    }

    public FileSystemResult<IReadOnlyList<string>> EnumerateDirectories(string directoryPath, string searchPattern = "*", bool recursive = false)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                return FileSystemResult<IReadOnlyList<string>>.Failure(OperationStatus.NotFound, $"Directory not found: {directoryPath}");
            }

            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            IReadOnlyList<string> directories = Directory.EnumerateDirectories(directoryPath, searchPattern, option).ToList();
            return FileSystemResult<IReadOnlyList<string>>.Ok(directories);
        }
        catch (Exception ex) when (TryClassify(ex, out var status))
        {
            return FileSystemResult<IReadOnlyList<string>>.Failure(status, ex.Message);
        }
    }

    public FileSystemResult<string> ReadLinkTarget(string path)
    {
        try
        {
            var target = new FileInfo(path).LinkTarget;
            return target is null
                ? FileSystemResult<string>.Failure(OperationStatus.NotFound, $"'{path}' is not a symbolic link.")
                : FileSystemResult<string>.Ok(target);
        }
        catch (Exception ex) when (TryClassify(ex, out var status))
        {
            return FileSystemResult<string>.Failure(status, ex.Message);
        }
    }

    private static bool TryClassify(Exception ex, out OperationStatus status)
    {
        status = ex switch
        {
            UnauthorizedAccessException => OperationStatus.AccessDenied,
            FileNotFoundException => OperationStatus.NotFound,
            DirectoryNotFoundException => OperationStatus.NotFound,
            IOException => OperationStatus.IoError,
            _ => OperationStatus.IoError
        };

        // Only these exception kinds are expected here; anything else is a genuine bug and
        // should surface as an unhandled exception rather than being silently swallowed.
        return ex is UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException or IOException;
    }
}
