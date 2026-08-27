using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.Services;

/// <summary>
/// GUI-3 §Step2: the ONLY boundary <see cref="ServerSleuth.Gui.ViewModels.ScanExecutionViewModel"/>
/// is allowed to depend on to actually run a scan — deliberately an interface, so nothing in
/// <c>ServerSleuth.Gui</c> itself (mechanically verified at the assembly-reference level by
/// <c>NoDirectPlatformAccessTests</c>) ever needs to know how a transport is built, how
/// discovery/analysis/risk/migration/reporting are wired together, or how a report is exported —
/// that composition lives entirely in the real implementation
/// (<c>ServerSleuth.Gui.ExecutionHost.GuiScanExecutor</c>), a SEPARATE assembly that is allowed
/// to reference <c>ServerSleuth.Infrastructure</c>/<c>ServerSleuth.Windows</c>/
/// <c>ServerSleuth.Reporting</c> because it is not "the GUI" in the sense that boundary protects
/// — it is the composition/execution host the GUI's own composition root wires in at startup.
///
/// <paramref name="credentials"/> is passed SEPARATELY from <paramref name="request"/>, never
/// merged into it — <see cref="ScanRequest"/> remains exactly as credential-free as GUI-2 made
/// it (skill.md GUI-3 §Step3's own suggested signature shape).
/// </summary>
public interface IGuiScanExecutor
{
    Task<ScanCompletionState> ExecuteAsync(
        ScanRequest request, ScanCredentialInput credentials, IProgress<ScanProgressState> progress, CancellationToken cancellationToken);
}
