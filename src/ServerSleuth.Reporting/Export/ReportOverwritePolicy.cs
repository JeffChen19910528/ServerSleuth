namespace ServerSleuth.Reporting.Export;

/// <summary>Explicit behavior when the target report file already exists — see skill.md
/// (Phase 9C) §8. <see cref="FailIfExists"/> is the default everywhere it's used in this codebase
/// — an export never silently overwrites a previous report.</summary>
public enum ReportOverwritePolicy
{
    FailIfExists,
    Overwrite
}
