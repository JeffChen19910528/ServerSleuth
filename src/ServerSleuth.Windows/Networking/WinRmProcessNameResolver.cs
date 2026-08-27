using ServerSleuth.Windows.Wmi;

namespace ServerSleuth.Windows.Networking;

/// <summary>
/// Satisfies the SAME <see cref="IProcessNameResolver"/> interface <see cref="WindowsPortInspector"/>
/// already depends on. Queries <c>Win32_Process</c> (Pid→Name) ONCE, lazily, on first call, and
/// caches the map for the lifetime of this instance (one per scan — see the target-aware
/// composition wiring) — never one remote query per port, avoiding the N+1 pattern skill.md
/// §29 forbids (a real Windows server can easily have dozens of listening ports, which would
/// otherwise mean dozens of remote round trips just to resolve process names).
/// </summary>
public sealed class WinRmProcessNameResolver(WinRmWmiOperations remoteWmi) : IProcessNameResolver
{
    private IReadOnlyDictionary<int, string>? _cache;

    public string? GetProcessName(int pid)
    {
        _cache ??= LoadNames();
        return _cache.GetValueOrDefault(pid);
    }

    private IReadOnlyDictionary<int, string> LoadNames()
    {
        var query = new WindowsWmiQuery
        {
            Namespace = WindowsWmiQuery.Cimv2Namespace,
            ClassName = "Win32_Process",
            Properties = ["ProcessId", "Name"]
        };

        var result = remoteWmi.Query(query);
        if (!result.Success || result.Value is null)
        {
            return new Dictionary<int, string>();
        }

        var names = new Dictionary<int, string>();
        foreach (var row in result.Value)
        {
            if (row.GetValueOrDefault("ProcessId") is IConvertible pidRaw && row.GetValueOrDefault("Name") is string name)
            {
                names[Convert.ToInt32(pidRaw)] = name;
            }
        }

        return names;
    }
}
