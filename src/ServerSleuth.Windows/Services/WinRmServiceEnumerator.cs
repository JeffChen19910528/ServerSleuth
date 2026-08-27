using ServerSleuth.Windows.Wmi;

namespace ServerSleuth.Windows.Services;

/// <summary>
/// Satisfies the SAME <see cref="IServiceEnumerator"/> interface <see cref="WindowsServiceScanner"/>
/// already depends on — the resolution to Phase 10D-3A's folded-in <c>ServiceQuery</c> decision
/// (skill.md Phase 10D-3B §16: "preserve that decision unless implementation proves it
/// impossible" — it did not). Uses <c>Win32_Service</c> (root\cimv2) instead of the LOCAL
/// enumerator's <c>ServiceController.GetServices()</c> SCM call. Structurally read-only — no
/// <c>Start</c>/<c>Stop</c>/<c>Delete</c>/<c>ChangeConfiguration</c> capability exists on
/// <see cref="IServiceEnumerator"/> or anywhere in this class.
///
/// <see cref="MapState"/>/<see cref="MapServiceType"/> translate <c>Win32_Service</c>'s
/// space-containing display strings (<c>"Start Pending"</c>, <c>"Own Process"</c>) into the
/// same PascalCase tokens <see cref="System.ServiceProcess.ServiceControllerStatus"/>/
/// <see cref="System.ServiceProcess.ServiceType"/>'s own <c>ToString()</c> already produces
/// locally (<c>"StartPending"</c>, <c>"Win32OwnProcess"</c>), so downstream risk-rule string
/// matching behaves the same for a local or remote scan. This mapping is NOT guaranteed
/// exhaustive for every possible WMI value — an unrecognized string is passed through verbatim
/// rather than dropped, a disclosed, honest best-effort (see ARCHITECTURE.md's Phase 10D-3B
/// addendum).
/// </summary>
public sealed class WinRmServiceEnumerator(WinRmWmiOperations remoteWmi) : IServiceEnumerator
{
    public IReadOnlyList<ServiceSnapshot> GetSnapshots()
    {
        var query = new WindowsWmiQuery
        {
            Namespace = WindowsWmiQuery.Cimv2Namespace,
            ClassName = "Win32_Service",
            Properties = ["Name", "DisplayName", "State", "ServiceType"]
        };

        var result = remoteWmi.Query(query);
        if (!result.Success || result.Value is null)
        {
            return [];
        }

        var snapshots = new List<ServiceSnapshot>();
        foreach (var row in result.Value)
        {
            if (row.GetValueOrDefault("Name") is not string name)
            {
                continue;
            }

            snapshots.Add(new ServiceSnapshot
            {
                ServiceName = name,
                DisplayName = row.GetValueOrDefault("DisplayName") as string ?? name,
                Status = MapState(row.GetValueOrDefault("State") as string),
                ServiceType = MapServiceType(row.GetValueOrDefault("ServiceType") as string)
            });
        }

        return snapshots;
    }

    private static string MapState(string? wmiState) => wmiState switch
    {
        "Running" => "Running",
        "Stopped" => "Stopped",
        "Paused" => "Paused",
        "Start Pending" => "StartPending",
        "Stop Pending" => "StopPending",
        "Continue Pending" => "ContinuePending",
        "Pause Pending" => "PausePending",
        null => "Unknown",
        _ => wmiState
    };

    private static string MapServiceType(string? wmiServiceType) => wmiServiceType switch
    {
        "Own Process" => "Win32OwnProcess",
        "Share Process" => "Win32ShareProcess",
        "Kernel Driver" => "KernelDriver",
        "File System Driver" => "FileSystemDriver",
        "Recognizer Driver" => "RecognizerDriver",
        "Adapter" => "Adapter",
        null => "Unknown",
        _ => wmiServiceType.Replace(" ", string.Empty, StringComparison.Ordinal)
    };
}
