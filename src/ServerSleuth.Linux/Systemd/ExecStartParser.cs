using System.Text.RegularExpressions;

namespace ServerSleuth.Linux.Systemd;

/// <summary>
/// Extracts the executable path from systemd's own `ExecStart` property shape, e.g.:
/// <c>{ path=/usr/bin/foo ; argv[]=/usr/bin/foo --flag ; ignore_errors=no ; start_time=... }</c>
/// Never guesses — an unrecognized shape yields null rather than a best-effort split.
/// See skill.md (Phase 6A) §8.
/// </summary>
public static partial class ExecStartParser
{
    [GeneratedRegex(@"path=(?<path>\S+)")]
    private static partial Regex PathPattern();

    public static string? ExtractExecutablePath(string? execStart)
    {
        if (execStart is null)
        {
            return null;
        }

        var match = PathPattern().Match(execStart);
        return match.Success ? match.Groups["path"].Value : null;
    }
}
