using ServerSleuth.Core.Enums;

namespace ServerSleuth.Linux.OperatingSystem;

/// <summary>Maps `uname -m` machine-hardware-name output to the cross-platform
/// EntityArchitecture enum. Unrecognized values map to Unknown — never guessed.</summary>
public static class LinuxArchitectureMapper
{
    public static EntityArchitecture FromUname(string? machine) => machine?.Trim() switch
    {
        "x86_64" or "amd64" => EntityArchitecture.X64,
        "i386" or "i486" or "i586" or "i686" => EntityArchitecture.X86,
        "aarch64" or "arm64" => EntityArchitecture.Arm64,
        var m when m is not null && m.StartsWith("arm", StringComparison.OrdinalIgnoreCase) => EntityArchitecture.Arm,
        _ => EntityArchitecture.Unknown
    };
}
