namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-5 §Export State: the terminal outcome of one "Export Report" action from the Results
/// Dashboard — a presentation-layer mirror of <c>ServerSleuth.Reporting.Export.ReportExportResult</c>/
/// <c>ReportBundleExportResult</c>, kept separate so <c>ServerSleuth.Gui</c> itself never needs to
/// reference <c>ServerSleuth.Reporting</c> directly (see <see cref="Services.IGuiReportExportService"/>'s
/// own doc comment for the boundary this preserves). Never fabricates success: <see cref="Success"/>
/// is only ever <c>true</c> when the underlying <c>IReportExporter</c> call itself reported success.
/// </summary>
public sealed record GuiReportExportResult
{
    public required bool Success { get; init; }

    /// <summary>The report file names actually written this call (e.g. <c>report.json</c>) — never
    /// a full absolute path beyond the directory the user themselves already supplied.</summary>
    public IReadOnlyList<string> WrittenFileNames { get; init; } = [];

    public GuiReportExportFailureReason FailureReason { get; init; } = GuiReportExportFailureReason.None;

    /// <summary>A concise, user-safe message only — never a raw exception message/stack trace
    /// (the same GUI-1 §8 error-boundary discipline every other GUI-facing message in this
    /// solution already follows).</summary>
    public string? ErrorMessage { get; init; }

    public static GuiReportExportResult Succeeded(IReadOnlyList<string> writtenFileNames) => new()
    {
        Success = true,
        WrittenFileNames = writtenFileNames
    };

    public static GuiReportExportResult Failed(GuiReportExportFailureReason reason, string errorMessage) => new()
    {
        Success = false,
        FailureReason = reason,
        ErrorMessage = errorMessage
    };
}
