using DiagProcess = System.Diagnostics.Process;

namespace ServerSleuth.Windows.Networking;

public sealed class ProcessNameResolver : IProcessNameResolver
{
    public string? GetProcessName(int pid)
    {
        try
        {
            using var process = DiagProcess.GetProcessById(pid);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null; // process has already exited
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
