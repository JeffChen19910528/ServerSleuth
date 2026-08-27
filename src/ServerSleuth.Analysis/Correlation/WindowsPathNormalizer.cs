using System.Text.RegularExpressions;

namespace ServerSleuth.Analysis.Correlation;

/// <summary>
/// The single reusable Windows path normalization strategy for correlation identity
/// resolution — see skill.md §5. Handles case differences, trailing separators, quoted paths,
/// and safely-resolvable environment-variable references, without ever guessing at an
/// unresolvable one. Deliberately does NOT collapse "Program Files" and "Program Files (x86)"
/// or merge distinct UNC hosts — it only case-folds and trims, never rewrites path segments.
/// </summary>
public static class WindowsPathNormalizer
{
    private static readonly Regex EnvVarPattern = new(@"%(?<name>[A-Za-z_][A-Za-z0-9_]*)%", RegexOptions.Compiled);

    public static NormalizedPath Normalize(string? rawPath, IEnvironmentVariableResolver? resolver = null)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return new NormalizedPath { OriginalPath = rawPath ?? string.Empty, Value = string.Empty, ComparisonKey = string.Empty };
        }

        var working = rawPath.Trim();

        if (working.Length >= 2 && working[0] == '"' && working[^1] == '"')
        {
            working = working[1..^1];
        }

        var (resolved, unresolved) = ResolveEnvironmentVariables(working, resolver ?? EnvironmentVariableResolver.Instance);

        var isUnc = resolved.StartsWith(@"\\", StringComparison.Ordinal);

        var canonical = resolved.Replace('/', '\\');
        canonical = canonical.TrimEnd('\\');

        return new NormalizedPath
        {
            OriginalPath = rawPath,
            Value = canonical,
            ComparisonKey = canonical.ToUpperInvariant(),
            IsUnc = isUnc,
            EnvironmentVariableUnresolved = unresolved
        };
    }

    /// <summary>
    /// Backslash-only directory-name extraction — deliberately NOT `System.IO.Path
    /// .GetDirectoryName`, since that BCL method interprets separators according to the
    /// *current* runtime OS, not the path's own origin: on a POSIX runtime it does not
    /// recognize `\` as a separator at all, silently breaking directory derivation for any
    /// Windows-sourced path when Analysis happens to run on Linux. Since every path reaching
    /// this method has already been through <see cref="Normalize"/> (backslash-canonical), a
    /// simple hand-rolled split is both correct and host-OS-independent — mirroring the same
    /// fix Phase 6B applied to Linux's own `LinuxPath` helper, in the opposite direction. Found
    /// via Phase 6G's real Linux (WSL Ubuntu) execution of the Analysis test suite, not assumed.
    /// </summary>
    public static string? GetDirectoryName(string? canonicalPath)
    {
        if (string.IsNullOrEmpty(canonicalPath))
        {
            return null;
        }

        var trimmed = canonicalPath.TrimEnd('\\');
        var lastSeparator = trimmed.LastIndexOf('\\');

        return lastSeparator switch
        {
            < 0 => null,
            0 => @"\",
            _ => trimmed[..lastSeparator]
        };
    }

    /// <summary>Backslash-only path join, for the same host-OS-independence reason as
    /// <see cref="GetDirectoryName"/>.</summary>
    public static string Combine(string directory, string fileName) => $"{directory.TrimEnd('\\')}\\{fileName}";

    /// <summary>Backslash-only file-name extraction, for the same host-OS-independence reason
    /// as <see cref="GetDirectoryName"/> — a Windows-style path's last segment must be derived
    /// the same way regardless of which OS is currently running this code.</summary>
    public static string GetFileName(string path)
    {
        var lastSeparator = path.LastIndexOf('\\');
        return lastSeparator < 0 ? path : path[(lastSeparator + 1)..];
    }

    private static (string Result, bool Unresolved) ResolveEnvironmentVariables(string path, IEnvironmentVariableResolver resolver)
    {
        var unresolved = false;

        var result = EnvVarPattern.Replace(path, match =>
        {
            var value = resolver.GetValue(match.Groups["name"].Value);
            if (value is null)
            {
                unresolved = true;
                return match.Value; // preserve the unresolved %NAME% reference verbatim — never guess
            }

            return value;
        });

        return (result, unresolved);
    }
}
