namespace ServerSleuth.Windows.Registry;

/// <summary>
/// A structured, read-only registry read request — the Phase 10D-3A capability-model
/// counterpart to <see cref="IWindowsRegistryReader"/>'s three existing local methods
/// (<c>GetSubKeyNames</c>/<c>GetValues</c>/<c>GetValue</c>), collapsed into ONE request shape a
/// future WinRM transport would accept, rather than three (skill.md §6: "the request should
/// describe WHAT to read, not HOW to read it"). <see cref="IncludeSubKeys"/> selects the
/// <c>GetSubKeyNames</c> behavior; <see cref="IncludeValues"/> (with <see cref="ValueNames"/>
/// empty, meaning "all") selects <c>GetValues</c>; <see cref="IncludeValues"/> with exactly one
/// name in <see cref="ValueNames"/> selects <c>GetValue</c>. Both flags may be set together
/// (a single round trip reading both a key's subkeys and its values, which
/// <see cref="ServerSleuth.Windows.COM.ComClsidReader"/> already effectively does today with
/// two separate local calls).
///
/// <see cref="Hive"/>/<see cref="View"/> reuse the existing <c>Microsoft.Win32.RegistryHive</c>/
/// <c>RegistryView</c> BCL enums — the exact same types <see cref="IWindowsRegistryReader"/>
/// already takes — rather than inventing parallel ones (skill.md §6's explicit instruction).
///
/// Deliberately has NO field for a value to write, no delete flag, no rename flag, no ACL
/// field, no import/export path — this request shape is structurally incapable of expressing
/// a registry mutation (skill.md §6's read-only requirement, enforced by the type itself, not
/// just by convention).
/// </summary>
public sealed record WindowsRegistryQuery
{
    public required Microsoft.Win32.RegistryHive Hive { get; init; }
    public required Microsoft.Win32.RegistryView View { get; init; }
    public required string KeyPath { get; init; }

    public bool IncludeSubKeys { get; init; }
    public bool IncludeValues { get; init; }

    /// <summary>Only meaningful when <see cref="IncludeValues"/> is <c>true</c>. Empty means
    /// "every named value under the key" (<c>GetValues</c>); one or more names restricts the
    /// read to exactly those values.</summary>
    public IReadOnlyList<string> ValueNames { get; init; } = [];

    public static WindowsRegistryQuery ForSubKeyNames(Microsoft.Win32.RegistryHive hive, Microsoft.Win32.RegistryView view, string keyPath) => new()
    {
        Hive = hive,
        View = view,
        KeyPath = keyPath,
        IncludeSubKeys = true
    };

    public static WindowsRegistryQuery ForAllValues(Microsoft.Win32.RegistryHive hive, Microsoft.Win32.RegistryView view, string keyPath) => new()
    {
        Hive = hive,
        View = view,
        KeyPath = keyPath,
        IncludeValues = true
    };

    public static WindowsRegistryQuery ForOneValue(Microsoft.Win32.RegistryHive hive, Microsoft.Win32.RegistryView view, string keyPath, string valueName) => new()
    {
        Hive = hive,
        View = view,
        KeyPath = keyPath,
        IncludeValues = true,
        ValueNames = [valueName]
    };
}
