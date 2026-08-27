using System.ComponentModel;
using DiagProcess = System.Diagnostics.Process;

namespace ServerSleuth.Windows.Process;

public sealed class ProcessEnumerator : IProcessEnumerator
{
    public IReadOnlyList<ProcessSnapshot> GetSnapshots()
    {
        var snapshots = new List<ProcessSnapshot>();

        foreach (var process in DiagProcess.GetProcesses())
        {
            using (process)
            {
                DateTimeOffset? startTime = null;
                var accessDenied = false;

                try
                {
                    startTime = process.StartTime;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or Win32Exception)
                {
                    accessDenied = true;
                }

                snapshots.Add(new ProcessSnapshot
                {
                    Pid = process.Id,
                    Name = process.ProcessName,
                    StartTime = startTime,
                    StartTimeAccessDenied = accessDenied
                });
            }
        }

        return snapshots;
    }
}
