namespace ServerSleuth.Linux.Common;

/// <summary>
/// Minimal, forward-slash-only path manipulation for Linux paths. `System.IO.Path` adapts to
/// the OS it's actually running on (e.g. `Path.GetDirectoryName` rewrites `/` to `\` when this
/// assembly happens to run on Windows) — since discovery data here is always Linux-shaped
/// regardless of the host running the analysis, path manipulation must not depend on the host
/// OS's separator conventions.
/// </summary>
internal static class LinuxPath
{
    public static string? GetDirectoryName(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var trimmed = path.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');

        return lastSlash switch
        {
            < 0 => null,
            0 => "/",
            _ => trimmed[..lastSlash]
        };
    }

    public static string Combine(string directory, string fileName) => $"{directory.TrimEnd('/')}/{fileName}";
}
