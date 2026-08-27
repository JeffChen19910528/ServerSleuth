namespace ServerSleuth.Reporting.Export;

/// <summary>
/// Validates that a report file name is safe to write under a caller-specified output directory
/// — see skill.md (Phase 9C) §7. Deliberately platform-INDEPENDENT: rather than relying on
/// <c>Path.GetInvalidFileNameChars()</c>/<c>Path.IsPathRooted</c> alone (both vary by host OS —
/// e.g. <c>Path.GetInvalidFileNameChars()</c> only returns NUL on Linux, so a Windows-dangerous
/// name like <c>"&lt;script&gt;"</c> would silently pass there), this checks an explicit,
/// fixed character/pattern blacklist so the same name is accepted or rejected identically on
/// Windows and Linux (skill.md §18).
///
/// This is never the only line of defense: <see cref="ReportArtifactFactory"/> only ever
/// constructs artifacts with the fixed default names (<c>report.json</c>/<c>report.html</c>) or
/// an explicitly-validated prefix, and no discovered entity name is ever used as a file name
/// anywhere in this codebase. This validator exists as defense in depth at the actual write
/// boundary, regardless of how a <see cref="ReportArtifact"/> was constructed.
/// </summary>
internal static class ReportFileNameValidator
{
    private static readonly char[] AlwaysInvalidChars = ['/', '\\', ':', '<', '>', '"', '|', '?', '*'];

    public static bool IsSafe(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName is "." or "..")
        {
            return false;
        }

        // Catches "../../evil", "..\..\evil", and any embedded traversal segment — not just a
        // leading one.
        if (fileName.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        if (fileName.Any(c => c < 0x20))
        {
            return false;
        }

        if (fileName.IndexOfAny(AlwaysInvalidChars) >= 0)
        {
            return false;
        }

        // Catches "C:\evil" and "/var/tmp/evil" on either host platform — Path.IsPathRooted's
        // own platform-dependent behavior is a secondary check here; the explicit ':' and '/'/
        // '\' checks above already reject both examples regardless of host OS.
        if (Path.IsPathRooted(fileName))
        {
            return false;
        }

        return true;
    }
}
