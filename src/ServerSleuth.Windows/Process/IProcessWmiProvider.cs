namespace ServerSleuth.Windows.Process;

public interface IProcessWmiProvider
{
    /// <summary>
    /// Returns whatever Win32_Process rows could be read, keyed by ProcessId. An empty/partial
    /// result (rather than an exception) is expected when WMI is unavailable or access is
    /// restricted — callers must still be able to report the ProcessSnapshot-only data.
    /// </summary>
    IReadOnlyDictionary<int, ProcessWmiInfo> GetAll();
}
