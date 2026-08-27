namespace ServerSleuth.Linux.Process;

/// <summary>Parses `/proc/&lt;pid&gt;/status`'s "Key:\tValue" line format. Never throws on a
/// malformed line — an unparsable line is simply skipped, and a missing "Name:" field is the
/// caller's signal to treat the entry as malformed rather than crash.</summary>
public static class ProcStatusParser
{
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in text.Split('\n'))
        {
            var separatorIndex = rawLine.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = rawLine[..separatorIndex].Trim();
            var value = rawLine[(separatorIndex + 1)..].Trim();
            result[key] = value;
        }

        return result;
    }

    /// <summary>The real Uid line has 4 tab-separated values (real, effective, saved, fs) — the
    /// first is the process's real UID.</summary>
    public static string? ExtractRealUid(string? uidLine)
    {
        if (uidLine is null)
        {
            return null;
        }

        var parts = uidLine.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }
}
