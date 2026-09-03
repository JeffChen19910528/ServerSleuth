using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Cli.Options;
using ServerSleuth.Reporting.Export;

namespace ServerSleuth.Cli.Pipeline;

/// <summary>
/// Renders and writes the requested report format(s) — see skill.md (Phase 10A) §10-11. Never
/// writes a file directly: every byte on disk goes through <see cref="ReportArtifactFactory"/>
/// and <see cref="IReportExporter"/> exactly as Phase 9C already built them. Uses
/// <see cref="IReportExporter.Export"/> per-artifact (rather than always calling
/// <see cref="IReportExporter.ExportBundle"/>) specifically so <c>--format json</c>/
/// <c>--format html</c> write only the requested file — <c>ReportArtifactFactory.CreateBundle</c>
/// still renders both formats internally (cheap, in-memory, and keeps the bundle's own JSON/HTML-
/// same-report guarantee intact), but only the requested artifact(s) are ever exported to disk.
///
/// GUI-9A: takes the full <see cref="ScanPipelineResult"/> (not just its <c>Report</c>) so the
/// inventory-aware <c>ReportArtifactFactory.CreateBundle(ScanPipelineResult, ...)</c> overload is
/// used — the CLI's JSON export previously called the <c>Report</c>-only overload, which left the
/// nine inventory list fields empty even though discovery had already populated them.
/// </summary>
public static class ReportExportRunner
{
    public static ScanExportOutcome Export(ScanPipelineResult pipelineResult, ScanOptions options)
    {
        var bundle = ReportArtifactFactory.CreateBundle(pipelineResult, language: options.Language);
        var exporter = new LocalFileReportExporter();

        var written = new List<string>();
        var diagnostics = new List<string>();
        var success = true;

        if (options.Format is ReportFormatOption.Json or ReportFormatOption.Both)
        {
            var result = exporter.Export(bundle.Json, options.OutputDirectory, options.OverwritePolicy);
            success &= result.Success;
            if (result.Success)
            {
                written.Add(bundle.Json.FileName);
            }
            else
            {
                diagnostics.AddRange(result.Diagnostics);
            }
        }

        if (options.Format is ReportFormatOption.Html or ReportFormatOption.Both)
        {
            var result = exporter.Export(bundle.Html, options.OutputDirectory, options.OverwritePolicy);
            success &= result.Success;
            if (result.Success)
            {
                written.Add(bundle.Html.FileName);
            }
            else
            {
                diagnostics.AddRange(result.Diagnostics);
            }
        }

        return new ScanExportOutcome { Success = success, WrittenFileNames = written, Diagnostics = diagnostics };
    }
}
