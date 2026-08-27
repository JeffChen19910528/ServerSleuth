using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Core.Targets;

namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-3 §Step11: the immutable state <see cref="ServerSleuth.Gui.ViewModels.ScanExecutionViewModel"/>
/// binds the Scan Execution view to — accumulated from a sequence of
/// <see cref="ScanProgressState"/> reports and finally a <see cref="ScanCompletionState"/>.
/// Deliberately contains ONLY non-sensitive data (skill.md §11's explicit "never store
/// Password/Credential/Secret/Token/PrivateKey/SecureString" list) — mechanically verified by
/// <c>NoCredentialShapedGuiStateTests</c>, the same test class GUI-1/GUI-2 already established.
/// </summary>
public sealed record ScanExecutionState
{
    public required ScanExecutionStatus Status { get; init; }

    public required ScanStage CurrentStage { get; init; }

    public string TargetDisplayName { get; init; } = string.Empty;

    public TargetPlatform TargetPlatform { get; init; } = TargetPlatform.Unknown;

    public IReadOnlyList<ScannerProgressInfo> ScannerStatuses { get; init; } = [];

    public int EntityCount { get; init; }

    public int ErrorCount { get; init; }

    public IReadOnlyList<string> OutputPaths { get; init; } = [];

    /// <summary>GUI-5 §Export/Report Viewer: the same output directory the user chose during Scan
    /// Configuration (<see cref="ScanRequest.OutputDirectory"/>) — a plain, already-user-supplied
    /// local path, never a credential. Needed so the Results Dashboard's post-hoc "Export Report"/
    /// "Open Report" actions know where <see cref="OutputPaths"/>' file names actually live,
    /// without re-deriving or re-prompting for it.</summary>
    public string OutputDirectory { get; init; } = string.Empty;

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>GUI-4 §Step2-3: the completed scan's own <see cref="ScanCompletionState.PipelineResult"/>,
    /// carried through unchanged — the single source of truth the Results Dashboard reads from.
    /// Never re-populated except by <see cref="WithCompletion"/>, so it stays exactly what the
    /// one real scan run produced regardless of how many times the user navigates to/from the
    /// dashboard afterward.</summary>
    public ScanPipelineResult? PipelineResult { get; init; }

    public static ScanExecutionState Idle { get; } = new() { Status = ScanExecutionStatus.Idle, CurrentStage = ScanStage.Preparing };

    public static ScanExecutionState StartingFor(ScanTarget target, string outputDirectory = "") => new()
    {
        Status = ScanExecutionStatus.Preparing,
        CurrentStage = ScanStage.Preparing,
        TargetDisplayName = target.DisplayName ?? target.Host ?? target.Id,
        TargetPlatform = target.Platform,
        OutputDirectory = outputDirectory,
        StartedAt = DateTimeOffset.UtcNow
    };

    public ScanExecutionState WithProgress(ScanProgressState progress) => this with
    {
        Status = ScanExecutionStatus.Running,
        CurrentStage = progress.Stage,
        ScannerStatuses = progress.ScannerStatuses.Count > 0 ? progress.ScannerStatuses : ScannerStatuses,
        EntityCount = progress.EntityCount ?? EntityCount
    };

    public ScanExecutionState WithCompletion(ScanCompletionState completion) => this with
    {
        Status = completion.Status,
        CurrentStage = completion.Status switch
        {
            ScanExecutionStatus.Cancelled => ScanStage.Cancelled,
            ScanExecutionStatus.Failed => ScanStage.Failed,
            _ => ScanStage.Completed
        },
        ScannerStatuses = completion.ScannerStatuses.Count > 0 ? completion.ScannerStatuses : ScannerStatuses,
        EntityCount = completion.EntityCount > 0 ? completion.EntityCount : EntityCount,
        ErrorCount = completion.ErrorCount,
        OutputPaths = completion.OutputPaths,
        FinishedAt = DateTimeOffset.UtcNow,
        ErrorMessage = completion.ErrorMessage,
        PipelineResult = completion.PipelineResult
    };
}
