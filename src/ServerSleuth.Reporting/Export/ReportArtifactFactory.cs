using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Json;

namespace ServerSleuth.Reporting.Export;

/// <summary>
/// Builds <see cref="ReportArtifact"/>/<see cref="ReportBundle"/> instances from an already-
/// produced <see cref="ServerMigrationAssessmentReport"/> — see skill.md (Phase 9C) §3, §9.
/// Performs no analysis of its own: it only invokes the existing <see cref="IReportRenderer"/>
/// implementations and wraps their <see cref="ReportRenderResult"/> with a safe file name.
/// </summary>
public static class ReportArtifactFactory
{
    public const string DefaultJsonFileName = "report.json";
    public const string DefaultHtmlFileName = "report.html";

    public static ReportArtifact FromRenderResult(ReportRenderResult result, string fileName)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!ReportFileNameValidator.IsSafe(fileName))
        {
            throw new ArgumentException($"'{fileName}' is not a safe report file name.", nameof(fileName));
        }

        return new ReportArtifact
        {
            FileName = fileName,
            Format = result.Format,
            Content = result.Content,
            Encoding = result.Encoding,
            ContentLength = result.Encoding.GetByteCount(result.Content)
        };
    }

    /// <summary>
    /// Renders JSON and HTML from the SAME <paramref name="report"/> instance and wraps both as
    /// one <see cref="ReportBundle"/> — never two separate analysis/render passes over different
    /// data (skill.md §9-10). <paramref name="filePrefix"/> is optional and sanitized via the
    /// same file-name safety check the exporter itself applies (§7) — an unsafe prefix throws
    /// immediately rather than silently stripping characters into something the caller didn't ask
    /// for.
    /// </summary>
    public static ReportBundle CreateBundle(ServerMigrationAssessmentReport report, string? filePrefix = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        var jsonFileName = filePrefix is null ? DefaultJsonFileName : $"{filePrefix}.json";
        var htmlFileName = filePrefix is null ? DefaultHtmlFileName : $"{filePrefix}.html";

        var jsonResult = new JsonReportRenderer().Render(report);
        var htmlResult = new HtmlReportRenderer().Render(report);

        return new ReportBundle
        {
            Json = FromRenderResult(jsonResult, jsonFileName),
            Html = FromRenderResult(htmlResult, htmlFileName)
        };
    }

    /// <summary>
    /// GUI-8C overload — produces an inventory-first HTML report by passing the full
    /// <paramref name="pipeline"/>'s discovery data to <see cref="HtmlReportRenderer"/> so it
    /// can render nine entity-type sections before the risk/migration sections. The JSON report
    /// uses <c>pipeline.Report</c> as before (existing JSON schema is forward-extended with
    /// optional inventory lists). Both artifacts are rendered from the same in-memory data;
    /// no second analysis pass is performed.
    /// </summary>
    public static ReportBundle CreateBundle(ScanPipelineResult pipeline, string? filePrefix = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        var jsonFileName = filePrefix is null ? DefaultJsonFileName : $"{filePrefix}.json";
        var htmlFileName = filePrefix is null ? DefaultHtmlFileName : $"{filePrefix}.html";

        var jsonResult = new JsonReportRenderer().Render(pipeline.Report);
        var htmlResult = new HtmlReportRenderer(
            discovery: pipeline.Discovery,
            boundaries: pipeline.Boundaries,
            externalDependencies: pipeline.ExternalDependencies).Render(pipeline.Report);

        return new ReportBundle
        {
            Json = FromRenderResult(jsonResult, jsonFileName),
            Html = FromRenderResult(htmlResult, htmlFileName)
        };
    }
}
