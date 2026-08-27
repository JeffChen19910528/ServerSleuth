using ServerSleuth.Core.Targets;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Windows.Wmi;

/// <summary>
/// The capability boundary a future WinRM transport must satisfy to serve
/// <see cref="WindowsWmiQuery"/> requests — see skill.md (Phase 10D-3A) §7, §14. The result is
/// a flat list of property-name→value rows (one per WMI instance matched), the same generic
/// shape <c>ManagementObjectSearcher</c> already produces locally — general enough to represent
/// both <c>Win32_Process</c> and <c>MSFT_NetTCPConnection</c>/<c>MSFT_NetUDPEndpoint</c> rows
/// without a dedicated result type per WMI class.
///
/// No implementation of this interface exists anywhere in this codebase yet (skill.md §3, §18,
/// §27: model only, no WinRM/WS-Man network call, no real WMI query execution).
/// </summary>
public interface IWindowsRemoteWmiOperations
{
    ScanTarget Target { get; }

    WindowsRemoteOperationResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>> Query(WindowsWmiQuery query);
}
