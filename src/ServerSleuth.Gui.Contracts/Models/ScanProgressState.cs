namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-3 §Step2, §Step6: one progress notification reported through <see cref="IProgress{T}"/>
/// (<c>ServerSleuth.Gui.Services.IGuiScanExecutor.ExecuteAsync</c>'s own
/// <c>IProgress&lt;ScanProgressState&gt;</c> parameter) — this project reuses the BCL's own
/// <see cref="IProgress{T}"/>/<see cref="Progress{T}"/> contract rather than inventing a second
/// progress vocabulary (skill.md GUI-3 §Step1's explicit instruction), since nothing in the
/// existing pipeline exposed one to reuse instead.
///
/// Deliberately carries only non-sensitive, already-computed data — no credential, no raw
/// exception, no fabricated percentage (skill.md §6, §10, §16).
/// </summary>
public sealed record ScanProgressState
{
    public required ScanStage Stage { get; init; }

    /// <summary>Set only while <see cref="Stage"/> is <see cref="ScanStage.Discovery"/> and at
    /// least one scanner has finished — the scanner-level detail skill.md §6 asks for. Empty
    /// (never fabricated) until real scanner results exist.</summary>
    public IReadOnlyList<ScannerProgressInfo> ScannerStatuses { get; init; } = [];

    /// <summary>The real entity count discovery has produced SO FAR, once known — <c>null</c>
    /// before discovery has reported anything, never an estimate.</summary>
    public int? EntityCount { get; init; }
}
