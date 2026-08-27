using ServerSleuth.Infrastructure.FileSystem;

namespace ServerSleuth.Linux.Runtimes;

public sealed class LinuxExecutableLocator(IFileSystemReader fileSystemReader) : IExecutableLocator
{
    public string? Locate(string fileName, IReadOnlyList<string> additionalDirectories)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathDirectories = pathValue.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in pathDirectories.Concat(additionalDirectories))
        {
            var candidate = $"{directory.TrimEnd('/')}/{fileName}";
            if (fileSystemReader.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
