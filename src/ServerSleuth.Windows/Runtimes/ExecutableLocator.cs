using ServerSleuth.Infrastructure.FileSystem;

namespace ServerSleuth.Windows.Runtimes;

public sealed class ExecutableLocator(IFileSystemReader fileSystemReader) : IExecutableLocator
{
    public string? Locate(string fileName, IReadOnlyList<string> additionalDirectories)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathDirectories = pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in pathDirectories.Concat(additionalDirectories))
        {
            var candidate = Path.Combine(directory, fileName);
            if (fileSystemReader.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
