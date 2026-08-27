using ServerSleuth.Windows.Wmi;

namespace ServerSleuth.Windows.Networking;

/// <summary>
/// Satisfies the SAME <see cref="INetworkTableProvider"/> interface <see cref="WindowsPortInspector"/>
/// already depends on — zero scanner code change needed (skill.md Phase 10D-3B §18, §20). Uses
/// the exact same <c>MSFT_NetTCPConnection</c>/<c>MSFT_NetUDPEndpoint</c> classes
/// (root\StandardCimv2) the LOCAL <see cref="NetworkTableProvider"/> already queries — the same
/// query shape, just carried over WinRM instead of an in-process WMI call.
/// </summary>
public sealed class WinRmNetworkTableProvider(WinRmWmiOperations remoteWmi) : INetworkTableProvider
{
    public IReadOnlyList<NetworkConnectionRow> GetListeningTcpEndpoints() => Query(
        "MSFT_NetTCPConnection", [new WmiFilterClause { PropertyName = "State", Operator = WmiComparisonOperator.Equals, Value = "2" }]);

    public IReadOnlyList<NetworkConnectionRow> GetUdpEndpoints() => Query("MSFT_NetUDPEndpoint", []);

    private IReadOnlyList<NetworkConnectionRow> Query(string className, IReadOnlyList<WmiFilterClause> filters)
    {
        var query = new WindowsWmiQuery
        {
            Namespace = WindowsWmiQuery.StandardCimv2Namespace,
            ClassName = className,
            Properties = ["LocalAddress", "LocalPort", "OwningProcess"],
            Filters = filters
        };

        var result = remoteWmi.Query(query);
        if (!result.Success || result.Value is null)
        {
            return [];
        }

        var rows = new List<NetworkConnectionRow>();
        foreach (var row in result.Value)
        {
            rows.Add(new NetworkConnectionRow
            {
                LocalAddress = row.GetValueOrDefault("LocalAddress") as string ?? string.Empty,
                LocalPort = row.GetValueOrDefault("LocalPort") is IConvertible portRaw ? Convert.ToInt32(portRaw) : 0,
                OwningProcessId = row.GetValueOrDefault("OwningProcess") is IConvertible pidRaw ? Convert.ToInt32(pidRaw) : null
            });
        }

        return rows;
    }
}
