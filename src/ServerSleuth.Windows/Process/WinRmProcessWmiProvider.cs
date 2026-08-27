using ServerSleuth.Windows.Wmi;

namespace ServerSleuth.Windows.Process;

/// <summary>
/// Satisfies the SAME <see cref="IProcessWmiProvider"/> interface <see cref="WindowsProcessScanner"/>
/// already depends on — zero scanner code change needed (skill.md Phase 10D-3B §18, §20).
///
/// **<see cref="ProcessWmiInfo.OwnerDomain"/>/<see cref="ProcessWmiInfo.OwnerUser"/> are ALWAYS
/// <c>null</c> here — a deliberate decision, not an oversight (skill.md §11, §29).** The local
/// <see cref="ProcessWmiProvider"/> resolves them via <c>Win32_Process.GetOwner()</c>, a
/// per-INSTANCE method invocation; <see cref="WindowsWmiMethodAllowList"/> proves this
/// capability CAN be represented structurally over WinRM (it is on the allow-list), but calling
/// it once per process discovered would turn one bulk process query into 1+N remote calls per
/// scan — exactly the N+1 anti-pattern skill.md §29 forbids trading against two optional,
/// nice-to-have fields. This is the resolution to Phase 10D-3A's disclosed <c>GetOwner</c> gap:
/// documented as permanently unavailable for remote process discovery, not silently guessed at
/// and not worked around with a fallback to local <c>System.Diagnostics.Process</c>.
/// </summary>
public sealed class WinRmProcessWmiProvider(WinRmWmiOperations remoteWmi) : IProcessWmiProvider
{
    public IReadOnlyDictionary<int, ProcessWmiInfo> GetAll()
    {
        var query = new WindowsWmiQuery
        {
            Namespace = WindowsWmiQuery.Cimv2Namespace,
            ClassName = "Win32_Process",
            Properties = ["ProcessId", "ExecutablePath", "CommandLine", "ParentProcessId"]
        };

        var result = remoteWmi.Query(query);
        if (!result.Success || result.Value is null)
        {
            return new Dictionary<int, ProcessWmiInfo>();
        }

        var byPid = new Dictionary<int, ProcessWmiInfo>();
        foreach (var row in result.Value)
        {
            if (row.GetValueOrDefault("ProcessId") is not IConvertible pidRaw)
            {
                continue;
            }

            var pid = Convert.ToInt32(pidRaw);
            byPid[pid] = new ProcessWmiInfo
            {
                ProcessId = pid,
                ExecutablePath = row.GetValueOrDefault("ExecutablePath") as string,
                CommandLine = row.GetValueOrDefault("CommandLine") as string,
                ParentProcessId = row.GetValueOrDefault("ParentProcessId") is IConvertible ppidRaw ? Convert.ToInt32(ppidRaw) : null,
                OwnerDomain = null,
                OwnerUser = null
            };
        }

        return byPid;
    }
}
