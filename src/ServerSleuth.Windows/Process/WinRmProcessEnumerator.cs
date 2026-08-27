using System.Management;
using ServerSleuth.Windows.Wmi;

namespace ServerSleuth.Windows.Process;

/// <summary>
/// Satisfies the SAME <see cref="IProcessEnumerator"/> interface <see cref="WindowsProcessScanner"/>
/// already depends on — this is the resolution to Phase 10D-3A's disclosed
/// <see cref="ProcessEnumerator"/> gap ("still hard-codes local
/// <c>System.Diagnostics.Process.GetProcesses()</c>, bypassing every injectable abstraction" —
/// skill.md Phase 10D-3B §12). Routes through <see cref="Wmi.IWindowsRemoteWmiOperations"/>
/// (<c>Win32_Process</c>) instead, exactly option B skill.md §12 offered — the LOCAL
/// <see cref="ProcessEnumerator"/> itself is untouched (a remote scan never registers it at all;
/// see <c>AddServerSleuthWindows</c>'s target-aware registration).
///
/// <see cref="System.Management.ManagementDateTimeConverter"/> parses <c>Win32_Process</c>'s CIM
/// datetime string format for <see cref="ProcessSnapshot.StartTime"/> — reused from the
/// pre-existing <c>System.Management</c> package reference (no new dependency).
/// </summary>
public sealed class WinRmProcessEnumerator(WinRmWmiOperations remoteWmi) : IProcessEnumerator
{
    public IReadOnlyList<ProcessSnapshot> GetSnapshots()
    {
        var query = new WindowsWmiQuery
        {
            Namespace = WindowsWmiQuery.Cimv2Namespace,
            ClassName = "Win32_Process",
            Properties = ["ProcessId", "Name", "CreationDate"]
        };

        var result = remoteWmi.Query(query);
        if (!result.Success || result.Value is null)
        {
            return [];
        }

        var snapshots = new List<ProcessSnapshot>();
        foreach (var row in result.Value)
        {
            if (row.GetValueOrDefault("ProcessId") is not IConvertible pidRaw || row.GetValueOrDefault("Name") is not string name)
            {
                continue;
            }

            DateTimeOffset? startTime = null;
            if (row.GetValueOrDefault("CreationDate") is string cimDateTime)
            {
                try
                {
                    startTime = ManagementDateTimeConverter.ToDateTime(cimDateTime);
                }
                catch (ArgumentException)
                {
                    // Malformed/unparseable CIM datetime — left unknown, never guessed at.
                }
            }

            snapshots.Add(new ProcessSnapshot
            {
                Pid = Convert.ToInt32(pidRaw),
                Name = name,
                StartTime = startTime,
                StartTimeAccessDenied = false
            });
        }

        return snapshots;
    }
}
