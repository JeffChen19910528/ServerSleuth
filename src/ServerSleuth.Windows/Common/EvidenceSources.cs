namespace ServerSleuth.Windows.Common;

/// <summary>Human-readable Source/Evidence.Location prefixes used consistently across Windows
/// scanners, so a report reader always sees the same wording for the same origin.</summary>
internal static class EvidenceSources
{
    public const string WindowsEnvironment = "Windows Environment";
    public const string WindowsRegistry = "Windows Registry";
    public const string WindowsProcessApi = "Windows Process API";
    public const string WindowsManagementInstrumentation = "WMI/CIM";
    public const string ServiceControlManager = "Windows Service Control Manager";
    public const string IisConfiguration = "IIS Configuration";
    public const string WindowsTaskScheduler = "Windows Task Scheduler";
    public const string WindowsCertificateStore = "Windows Certificate Store";
    public const string FileSystem = "FileSystem";
}
