using ServerSleuth.Core.Targets;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Windows.IIS;

/// <summary>
/// The capability boundary a future WinRM transport must satisfy to serve an IIS configuration
/// snapshot — see skill.md (Phase 10D-3A) §9, §14. Deliberately parameterless (like
/// <see cref="IIisConfigurationProvider.GetSnapshot"/>, the local interface this mirrors) —
/// nothing about the current IIS scanner scopes its read to a subset of sites/pools, so the
/// remote capability does not invent a filtering parameter no caller needs. Returns the SAME
/// <see cref="IisSnapshot"/> the local provider already returns — reused directly rather than a
/// duplicate DTO, since the shape is already fully structured (skill.md §9: "possible
/// conceptual fields include Sites/Applications/ApplicationPools/Bindings/VirtualDirectories" —
/// all already present on <see cref="IisSiteRow"/>/<see cref="IisAppPoolRow"/>/
/// <see cref="IisApplicationRow"/>/<see cref="IisBindingRow"/>).
///
/// Exposes no method capable of mutating IIS configuration (no <c>Start</c>/<c>Stop</c>/
/// <c>Recycle</c>/<c>SetProperty</c> of any kind) — read-only by construction, matching every
/// other capability interface in this model.
///
/// No implementation of this interface exists anywhere in this codebase yet (skill.md §3, §18,
/// §27: model only).
/// </summary>
public interface IWindowsRemoteIisOperations
{
    ScanTarget Target { get; }

    WindowsRemoteOperationResult<IisSnapshot> GetSnapshot();
}
