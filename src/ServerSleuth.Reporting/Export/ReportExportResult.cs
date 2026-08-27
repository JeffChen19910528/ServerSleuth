namespace ServerSleuth.Reporting.Export;

/// <summary>
/// The outcome of exporting one <see cref="ReportArtifact"/> — see skill.md (Phase 9C) §4.
/// Expected export failures (existing file with <see cref="ReportOverwritePolicy.FailIfExists"/>,
/// an inaccessible/invalid output directory, an unsafe file name) are represented here rather
/// than thrown — only a genuine programming-error exception (e.g. a null argument) throws.
/// <see cref="Success"/> is <c>false</c> whenever <see cref="OutputPath"/> does not, in fact,
/// exist on disk with the exported content — this type is never used to claim success when the
/// final file wasn't actually written (skill.md §17).
/// </summary>
public sealed record ReportExportResult
{
    public required bool Success { get; init; }
    public required ReportFormat Format { get; init; }

    /// <summary>The full path written, only when <see cref="Success"/> is <c>true</c>.</summary>
    public string? OutputPath { get; init; }

    public long BytesWritten { get; init; }

    /// <summary>Human-readable diagnostic messages — populated on failure, explaining exactly
    /// why the export did not succeed.</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}
