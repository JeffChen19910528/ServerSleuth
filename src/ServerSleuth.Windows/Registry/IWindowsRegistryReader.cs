using Microsoft.Win32;

namespace ServerSleuth.Windows.Registry;

/// <summary>
/// Read-only registry access used by Windows scanners instead of raw Microsoft.Win32.Registry
/// calls, so AccessDenied/NotFound are handled uniformly and a single bad key never crashes a
/// scan. See skill.md §25-26.
/// </summary>
public interface IWindowsRegistryReader
{
    RegistryResult<IReadOnlyList<string>> GetSubKeyNames(RegistryHive hive, RegistryView view, string path);

    /// <summary>Reads every named value under a key as a flat name→value map (missing values
    /// are simply absent from the map, never inferred).</summary>
    RegistryResult<IReadOnlyDictionary<string, object?>> GetValues(RegistryHive hive, RegistryView view, string path);

    RegistryResult<object?> GetValue(RegistryHive hive, RegistryView view, string path, string valueName);
}
