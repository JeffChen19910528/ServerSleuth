using System.Text.RegularExpressions;

namespace ServerSleuth.Linux.Cron;

/// <summary>
/// Parses one crontab line — never evaluates shell syntax, never expands anything. Comments,
/// blank lines, and environment-variable assignment lines (e.g. `PATH=/usr/bin:/bin`,
/// `MAILTO=""`) are recognized and skipped rather than misparsed as malformed jobs. See
/// skill.md (Phase 6B) §17-18, §21.
/// </summary>
public static partial class CronLineParser
{
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*\s*=")]
    private static partial Regex EnvAssignmentPattern();

    /// <summary>User crontab format (5 time fields, no user column) — e.g. `/var/spool/cron/crontabs/&lt;user&gt;`.</summary>
    public static CronEntry? ParseUserCrontabLine(string line)
    {
        var trimmed = line.Trim();
        if (IsSkippable(trimmed))
        {
            return null;
        }

        if (trimmed.StartsWith('@'))
        {
            var parts = trimmed.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length < 2 ? null : new CronEntry { Schedule = parts[0], Command = parts[1] };
        }

        var fields = trimmed.Split((char[]?)null, 6, StringSplitOptions.RemoveEmptyEntries);
        return fields.Length < 6 ? null : new CronEntry { Schedule = string.Join(' ', fields[..5]), Command = fields[5] };
    }

    /// <summary>System crontab format (5 time fields + user column) — `/etc/crontab` and
    /// `/etc/cron.d/*`.</summary>
    public static CronEntry? ParseSystemCrontabLine(string line)
    {
        var trimmed = line.Trim();
        if (IsSkippable(trimmed))
        {
            return null;
        }

        if (trimmed.StartsWith('@'))
        {
            var parts = trimmed.Split((char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length < 3 ? null : new CronEntry { Schedule = parts[0], User = parts[1], Command = parts[2] };
        }

        var fields = trimmed.Split((char[]?)null, 7, StringSplitOptions.RemoveEmptyEntries);
        return fields.Length < 7 ? null : new CronEntry { Schedule = string.Join(' ', fields[..5]), User = fields[5], Command = fields[6] };
    }

    private static bool IsSkippable(string trimmed) =>
        trimmed.Length == 0 || trimmed.StartsWith('#') || EnvAssignmentPattern().IsMatch(trimmed);
}
