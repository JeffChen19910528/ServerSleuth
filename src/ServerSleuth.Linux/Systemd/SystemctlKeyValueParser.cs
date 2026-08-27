namespace ServerSleuth.Linux.Systemd;

/// <summary>Parses `systemctl show`'s machine-readable `Key=Value` per-line output — never
/// scrapes `systemctl status`'s human-formatted prose. See skill.md (Phase 6A) §7.</summary>
public static class SystemctlKeyValueParser
{
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex];
            var value = line[(separatorIndex + 1)..];
            result[key] = value;
        }

        return result;
    }
}
