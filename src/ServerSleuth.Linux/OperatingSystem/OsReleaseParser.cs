namespace ServerSleuth.Linux.OperatingSystem;

/// <summary>Parses the standard `/etc/os-release` KEY=VALUE format (values optionally quoted) —
/// see os-release(5). Never throws on malformed input; unparsable lines are simply skipped.</summary>
public static class OsReleaseParser
{
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1];
            }

            result[key] = value;
        }

        return result;
    }
}
