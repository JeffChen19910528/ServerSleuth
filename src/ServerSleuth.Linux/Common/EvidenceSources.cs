namespace ServerSleuth.Linux.Common;

/// <summary>Human-readable Source/Evidence.Location prefixes used consistently across Linux
/// scanners, mirroring the Windows project's convention.</summary>
internal static class EvidenceSources
{
    public const string OsRelease = "/etc/os-release";
    public const string ProcSysKernel = "/proc/sys/kernel";
    public const string ProcFilesystem = "/proc";
    public const string ProcNet = "/proc/net";
    public const string Systemd = "systemd";
    public const string Command = "Command";
    public const string PackageManager = "PackageManager";
    public const string Cron = "Cron";
    public const string FileSystem = "FileSystem";
}
