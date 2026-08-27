namespace ServerSleuth.Infrastructure.Runtimes;

/// <summary>
/// Parses a single line of `dotnet --list-runtimes`/`--list-sdks` output — identical text
/// shape on both Windows and Linux, so this is the one place the parsing rule is defined.
/// e.g. "Microsoft.NETCore.App 8.0.11 [/usr/share/dotnet/shared/Microsoft.NETCore.App]"
/// (list-runtimes) or "8.0.400 [/usr/share/dotnet/sdk]" (list-sdks).
/// </summary>
public static class DotNetCliOutputParser
{
    public static (string? Name, string? Version, string? Path) ParseLine(string line)
    {
        var bracketIndex = line.IndexOf('[');
        var beforeBracket = (bracketIndex >= 0 ? line[..bracketIndex] : line).Trim();
        var path = bracketIndex >= 0 && line.TrimEnd().EndsWith(']')
            ? line[(bracketIndex + 1)..line.LastIndexOf(']')].Trim()
            : null;

        var parts = beforeBracket.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            >= 2 => (string.Join(' ', parts[..^1]), parts[^1], path), // "Name Version"
            1 => (null, parts[0], path), // SDK lines are just "Version"
            _ => (null, null, path)
        };
    }
}
