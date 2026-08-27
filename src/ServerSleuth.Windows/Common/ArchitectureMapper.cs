using System.Runtime.InteropServices;
using ServerSleuth.Core.Enums;

namespace ServerSleuth.Windows.Common;

internal static class ArchitectureMapper
{
    public static EntityArchitecture FromRuntimeArchitecture(Architecture architecture) => architecture switch
    {
        Architecture.X86 => EntityArchitecture.X86,
        Architecture.X64 => EntityArchitecture.X64,
        Architecture.Arm => EntityArchitecture.Arm,
        Architecture.Arm64 => EntityArchitecture.Arm64,
        _ => EntityArchitecture.Unknown
    };
}
