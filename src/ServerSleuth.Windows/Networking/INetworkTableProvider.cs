namespace ServerSleuth.Windows.Networking;

/// <summary>
/// Reads listening TCP/UDP endpoints from the OS. Backed by the MSFT_NetTCPConnection /
/// MSFT_NetUDPEndpoint CIM classes (root\StandardCimv2), which — unlike the classic
/// System.Net.NetworkInformation APIs — expose the owning process id directly, so scanners
/// never need to shell out to netstat and parse text. See skill.md §13.
/// </summary>
public interface INetworkTableProvider
{
    IReadOnlyList<NetworkConnectionRow> GetListeningTcpEndpoints();

    IReadOnlyList<NetworkConnectionRow> GetUdpEndpoints();
}
