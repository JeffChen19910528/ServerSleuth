using System.Runtime.InteropServices;

namespace ServerSleuth.Windows.OperatingSystem;

/// <summary>
/// The subset of Environment/RuntimeInformation values the OS scanner needs, captured as data
/// so the mapping logic in WindowsOsScanner can be unit-tested with a synthetic snapshot
/// instead of the real machine.
/// </summary>
public sealed record EnvironmentSnapshot
{
    public required string MachineName { get; init; }
    public required string OsDescription { get; init; }
    public required Architecture OsArchitecture { get; init; }
    public required string FrameworkDescription { get; init; }
    public required string SystemDirectory { get; init; }
    public required string UserName { get; init; }
    public required string UserDomainName { get; init; }

    public static EnvironmentSnapshot Capture() => new()
    {
        MachineName = Environment.MachineName,
        OsDescription = RuntimeInformation.OSDescription,
        OsArchitecture = RuntimeInformation.OSArchitecture,
        FrameworkDescription = RuntimeInformation.FrameworkDescription,
        SystemDirectory = Environment.SystemDirectory,
        UserName = Environment.UserName,
        UserDomainName = Environment.UserDomainName
    };
}
