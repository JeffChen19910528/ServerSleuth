using ServerSleuth.Analysis.Orchestration;

namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-3 §Step2, §Step11: the terminal snapshot <c>IGuiScanExecutor.ExecuteAsync</c> returns —
/// exactly one of these is produced per execution, always as the LAST thing that happens.
/// <see cref="ErrorMessage"/>, when set, is always the SAME fixed, generic, credential-free text
/// the existing <c>GuiExceptionHandler</c> convention already established for GUI-1 (skill.md
/// §10: "do not display stack traces to normal users... user-visible errors should be concise
/// and generic") — never a raw <see cref="Exception.Message"/>.
/// </summary>
public sealed record ScanCompletionState
{
    public required ScanExecutionStatus Status { get; init; }

    public int EntityCount { get; init; }

    public int ErrorCount { get; init; }

    public IReadOnlyList<ScannerProgressInfo> ScannerStatuses { get; init; } = [];

    /// <summary>The report file names actually written (e.g. <c>report.json</c>) — never a full
    /// absolute path with unrelated directory structure beyond what the user themselves already
    /// typed as the output directory.</summary>
    public IReadOnlyList<string> OutputPaths { get; init; } = [];

    public string? ErrorMessage { get; init; }

    /// <summary>GUI-4 §Step2: the SAME <see cref="ScanPipelineResult"/> instance
    /// <c>GuiScanExecutor</c> already produced (Risk Aggregation + the consolidated Migration
    /// Assessment Report) — never a copy, never re-derived. Null for
    /// <see cref="Cancelled"/>/<see cref="Failed"/> (and for any completion where the pipeline
    /// never reached the point of producing a report) — the Results Dashboard is required to
    /// handle that null case as an empty/partial result (skill.md GUI-4 §19), never by
    /// re-running anything to "fill it in."</summary>
    public ScanPipelineResult? PipelineResult { get; init; }

    public static ScanCompletionState Cancelled() => new() { Status = ScanExecutionStatus.Cancelled };

    public static ScanCompletionState Failed(string errorMessage) => new() { Status = ScanExecutionStatus.Failed, ErrorMessage = errorMessage };
}
