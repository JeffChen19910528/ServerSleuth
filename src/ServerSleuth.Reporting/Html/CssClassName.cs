using System.Text.RegularExpressions;

namespace ServerSleuth.Reporting.Html;

/// <summary>
/// Converts an existing status/severity/coverage enum string (e.g. <c>"NeedsRemediation"</c>)
/// into a CSS-safe class name (<c>"needs-remediation"</c>) — see skill.md (Phase 9B) §6-7:
/// "CSS classes must be derived from the existing status value only... do not introduce new
/// status semantics." Purely a syntactic transform (PascalCase -&gt; kebab-case) of a value the
/// DTO already carries; it never branches on, maps, or invents a new classification.
/// </summary>
internal static class CssClassName
{
    private static readonly Regex UpperBoundary = new("(?<!^)([A-Z])", RegexOptions.Compiled);

    public static string From(string value) => UpperBoundary.Replace(value, "-$1").ToLowerInvariant();
}
