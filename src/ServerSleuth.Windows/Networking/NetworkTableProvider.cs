using System.Management;
using Microsoft.Extensions.Logging;

namespace ServerSleuth.Windows.Networking;

public sealed class NetworkTableProvider(ILogger<NetworkTableProvider> logger) : INetworkTableProvider
{
    private static readonly ManagementScope Scope = new(@"root\StandardCimv2");

    public IReadOnlyList<NetworkConnectionRow> GetListeningTcpEndpoints() =>
        Query("SELECT LocalAddress, LocalPort, OwningProcess FROM MSFT_NetTCPConnection WHERE State = 2", "MSFT_NetTCPConnection");

    public IReadOnlyList<NetworkConnectionRow> GetUdpEndpoints() =>
        Query("SELECT LocalAddress, LocalPort, OwningProcess FROM MSFT_NetUDPEndpoint", "MSFT_NetUDPEndpoint");

    private IReadOnlyList<NetworkConnectionRow> Query(string wql, string className)
    {
        var rows = new List<NetworkConnectionRow>();

        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, new ObjectQuery(wql));
            using var collection = searcher.Get();

            foreach (ManagementBaseObject item in collection)
            {
                using var mo = item;

                rows.Add(new NetworkConnectionRow
                {
                    LocalAddress = mo["LocalAddress"] as string ?? string.Empty,
                    LocalPort = Convert.ToInt32(mo["LocalPort"]),
                    OwningProcessId = mo["OwningProcess"] is not null ? Convert.ToInt32(mo["OwningProcess"]) : null
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} query failed; listening endpoints for this protocol will be unavailable.", className);
        }

        return rows;
    }
}
