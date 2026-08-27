using ServerSleuth.Core.Targets;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Windows.Registry;

/// <summary>
/// The capability boundary a future WinRM transport must satisfy to serve
/// <see cref="WindowsRegistryQuery"/> requests against a <see cref="TargetKind.Remote"/>,
/// <see cref="TargetPlatform.Windows"/> <see cref="ScanTarget"/> — see skill.md (Phase 10D-3A)
/// §14. Exactly one method, exactly one structured request/result pair, exactly like
/// <see cref="ServerSleuth.Infrastructure.Process.IProcessRunner"/>'s own single-method shape —
/// never a shape that could accept a raw registry-path-plus-command string.
///
/// No implementation of this interface exists anywhere in this codebase yet (skill.md §3, §18,
/// §27: model only). A future WinRM-backed implementation would sit beside, not replace,
/// <see cref="IWindowsRegistryReader"/> — the LOCAL scanner-facing interface — the same way
/// <see cref="ServerSleuth.Infrastructure.Remote.SshProcessRunner"/> sits beside the local
/// <c>ProcessRunner</c> as an alternate <c>IProcessRunner</c> implementation, never a
/// replacement for the interface scanners already depend on.
/// </summary>
public interface IWindowsRemoteRegistryOperations
{
    ScanTarget Target { get; }

    WindowsRemoteOperationResult<WindowsRegistryQueryResult> Query(WindowsRegistryQuery query);
}
