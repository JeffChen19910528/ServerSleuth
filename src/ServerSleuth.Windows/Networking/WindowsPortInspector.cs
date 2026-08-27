using ServerSleuth.Infrastructure.Networking;

namespace ServerSleuth.Windows.Networking;

/// <summary>
/// Windows implementation of the cross-platform IPortInspector contract (defined in Phase 2).
/// Sourced from the MSFT_NetTCPConnection / MSFT_NetUDPEndpoint CIM classes rather than
/// netstat text parsing — see skill.md §13 ("the actual owning process is authoritative").
/// </summary>
public sealed class WindowsPortInspector(INetworkTableProvider tableProvider, IProcessNameResolver processNameResolver)
    : IPortInspector
{
    public Task<IReadOnlyList<NetworkEndpoint>> GetListeningEndpointsAsync(CancellationToken cancellationToken)
    {
        var endpoints = new List<NetworkEndpoint>();

        foreach (var row in tableProvider.GetListeningTcpEndpoints())
        {
            cancellationToken.ThrowIfCancellationRequested();
            endpoints.Add(BuildEndpoint(row, "TCP"));
        }

        foreach (var row in tableProvider.GetUdpEndpoints())
        {
            cancellationToken.ThrowIfCancellationRequested();
            endpoints.Add(BuildEndpoint(row, "UDP"));
        }

        return Task.FromResult<IReadOnlyList<NetworkEndpoint>>(endpoints);
    }

    private NetworkEndpoint BuildEndpoint(NetworkConnectionRow row, string protocol) => new()
    {
        Protocol = protocol,
        LocalAddress = row.LocalAddress,
        LocalPort = row.LocalPort,
        ProcessId = row.OwningProcessId,
        ProcessName = row.OwningProcessId.HasValue ? processNameResolver.GetProcessName(row.OwningProcessId.Value) : null,
        State = "Listening"
    };
}
