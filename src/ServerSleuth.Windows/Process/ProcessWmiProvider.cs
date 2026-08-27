using System.Management;
using Microsoft.Extensions.Logging;

namespace ServerSleuth.Windows.Process;

public sealed class ProcessWmiProvider(ILogger<ProcessWmiProvider> logger) : IProcessWmiProvider
{
    public IReadOnlyDictionary<int, ProcessWmiInfo> GetAll()
    {
        var results = new Dictionary<int, ProcessWmiInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, ExecutablePath, CommandLine, ParentProcessId FROM Win32_Process");
            using var collection = searcher.Get();

            foreach (ManagementBaseObject item in collection)
            {
                using var mo = (ManagementObject)item;

                var processId = Convert.ToInt32(mo["ProcessId"]);
                var (ownerDomain, ownerUser) = TryGetOwner(mo);

                results[processId] = new ProcessWmiInfo
                {
                    ProcessId = processId,
                    ExecutablePath = mo["ExecutablePath"] as string,
                    CommandLine = mo["CommandLine"] as string,
                    ParentProcessId = mo["ParentProcessId"] is uint ppid ? (int)ppid : null,
                    OwnerDomain = ownerDomain,
                    OwnerUser = ownerUser
                };
            }
        }
        catch (Exception ex)
        {
            // WMI can be entirely unavailable/locked down in some environments — the caller
            // falls back to ProcessSnapshot-only data for every process rather than failing
            // the whole scan.
            logger.LogWarning(ex, "Win32_Process query failed; process metadata will be limited to what System.Diagnostics.Process exposes.");
        }

        return results;
    }

    private static (string? Domain, string? User) TryGetOwner(ManagementObject process)
    {
        try
        {
            using var outParams = process.InvokeMethod("GetOwner", null) as ManagementBaseObject;
            if (outParams is not null && Convert.ToUInt32(outParams["ReturnValue"]) == 0)
            {
                return (outParams["Domain"] as string, outParams["User"] as string);
            }
        }
        catch (ManagementException)
        {
            // Expected for protected/system processes — owner stays unknown, not an error.
        }

        return (null, null);
    }
}
