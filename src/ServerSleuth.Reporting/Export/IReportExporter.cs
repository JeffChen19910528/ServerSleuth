namespace ServerSleuth.Reporting.Export;

/// <summary>
/// Persists an already-rendered <see cref="ReportArtifact"/>/<see cref="ReportBundle"/> to some
/// destination — see skill.md (Phase 9C) §2, §4. An exporter never performs analysis, never
/// re-renders, and never mutates its input; it only writes bytes that were already computed
/// upstream. <see cref="LocalFileReportExporter"/> is the first (and, as of Phase 9C, only)
/// implementation.
/// </summary>
public interface IReportExporter
{
    ReportExportResult Export(ReportArtifact artifact, string outputDirectory, ReportOverwritePolicy overwritePolicy = ReportOverwritePolicy.FailIfExists);

    /// <summary>
    /// Exports both artifacts in <paramref name="bundle"/> — always the JSON and HTML rendered
    /// from the same report instance (skill.md §10) — and, when <paramref name="includeManifest"/>
    /// is <c>true</c>, a <c>report-manifest.json</c> alongside them (skill.md §11, opt-in, never
    /// generated unless explicitly requested). <paramref name="manifestCreatedAt"/> is likewise
    /// opt-in; when omitted, the manifest carries no creation timestamp at all.
    /// </summary>
    ReportBundleExportResult ExportBundle(
        ReportBundle bundle,
        string outputDirectory,
        ReportOverwritePolicy overwritePolicy = ReportOverwritePolicy.FailIfExists,
        bool includeManifest = false,
        DateTimeOffset? manifestCreatedAt = null);
}
