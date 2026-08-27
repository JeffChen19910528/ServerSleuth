using ServerSleuth.Core.Enums;

namespace ServerSleuth.Gui.Models;

/// <summary>GUI-3 §Step6: one scanner's own real, already-computed outcome — <see cref="ScannerId"/>/
/// <see cref="Status"/>/<see cref="EntityCount"/> are read directly off
/// <see cref="ServerSleuth.Core.Results.DiscoveryResult"/> (see
/// <see cref="ServerSleuth.Gui.ExecutionHost.GuiScanExecutor"/>) — never fabricated, never a
/// synthetic/estimated count. <see cref="Status"/> reuses the EXISTING
/// <see cref="ScannerStatus"/> enum directly (it already lives in <c>ServerSleuth.Core</c>, an
/// allowed GUI dependency — no mirror type needed here, unlike GUI-2's Infrastructure/Cli/
/// Reporting/Windows-only enums).</summary>
public sealed record ScannerProgressInfo
{
    public required string ScannerId { get; init; }
    public required ScannerStatus Status { get; init; }
    public required int EntityCount { get; init; }
}
