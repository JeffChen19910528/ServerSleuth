using System.Text.RegularExpressions;

namespace ServerSleuth.Linux.Packages;

/// <summary>
/// Parses one line of `apk info -v` output, e.g. "musl-1.2.4-r2" or "ca-certificates-20230506-r0".
/// Alpine package versions always end in the well-known "-r&lt;release&gt;" suffix convention,
/// which is the only reliable place to split name from version — a plain "last hyphen" split
/// would break on names like "ca-certificates". A line that doesn't match this shape is skipped
/// rather than guessed at.
/// </summary>
public static partial class ApkPackageLineParser
{
    [GeneratedRegex(@"^(?<name>.+)-(?<version>[0-9][^-]*-r[0-9]+)$")]
    private static partial Regex PackagePattern();

    public static (string Name, string Version)? Parse(string line)
    {
        var match = PackagePattern().Match(line.Trim());
        return match.Success ? (match.Groups["name"].Value, match.Groups["version"].Value) : null;
    }
}
