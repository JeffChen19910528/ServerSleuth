using System.Text.RegularExpressions;
using ServerSleuth.Infrastructure.FileSystem;

namespace ServerSleuth.Linux.Networking;

public sealed partial class SocketOwnershipResolver(IFileSystemReader fileSystemReader) : ISocketOwnershipResolver
{
    [GeneratedRegex(@"^socket:\[(?<inode>\d+)\]$")]
    private static partial Regex SocketLinkPattern();

    public IReadOnlyDictionary<string, int> BuildInodeToPidMap()
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);

        var directoriesResult = fileSystemReader.EnumerateDirectories("/proc", "*");
        if (!directoriesResult.Success)
        {
            return map;
        }

        foreach (var directory in directoriesResult.Value!)
        {
            var name = Path.GetFileName(directory.TrimEnd('/'));
            if (!int.TryParse(name, out var pid))
            {
                continue;
            }

            var fdResult = fileSystemReader.EnumerateFiles($"/proc/{pid}/fd", "*");
            if (!fdResult.Success)
            {
                continue; // AccessDenied for another user's process is expected, not an error
            }

            foreach (var fdPath in fdResult.Value!)
            {
                var target = TryResolveLinkTarget(fdPath);
                if (target is null)
                {
                    continue;
                }

                var match = SocketLinkPattern().Match(target);
                if (match.Success)
                {
                    map.TryAdd(match.Groups["inode"].Value, pid);
                }
            }
        }

        return map;
    }

    private static string? TryResolveLinkTarget(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
