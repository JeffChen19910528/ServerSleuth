using ServerSleuth.Core.Enums;

namespace ServerSleuth.Windows.Runtimes;

/// <summary>
/// Cheap, non-executing architecture heuristic based on well-known install-path conventions
/// (e.g. "Program Files (x86)"). Deliberately does not parse the executable's PE header —
/// that is the same "DLL deep analysis" judged out of scope in Phase 4B and is not attempted
/// here either; an unrecognized path shape stays Unknown rather than guessing.
/// </summary>
public static class ArchitectureHint
{
    public static EntityArchitecture FromPath(string? path)
    {
        if (path is null)
        {
            return EntityArchitecture.Unknown;
        }

        if (path.Contains("Program Files (x86)", StringComparison.OrdinalIgnoreCase))
        {
            return EntityArchitecture.X86;
        }

        if (path.Contains("Program Files", StringComparison.OrdinalIgnoreCase))
        {
            return EntityArchitecture.X64;
        }

        return EntityArchitecture.Unknown;
    }
}
