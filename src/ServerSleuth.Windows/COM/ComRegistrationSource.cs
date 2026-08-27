using Microsoft.Win32;

namespace ServerSleuth.Windows.COM;

/// <summary>
/// The three CLSID registry locations named in skill.md §9. Registry64/Registry32 views are
/// used (not a manually hard-coded "\WOW6432Node\" path) so the OS registry redirector does
/// the 32-bit mapping — the same convention WindowsInstalledSoftwareScanner already
/// established in Phase 3 for the Uninstall keys. See skill.md §23's explicit instruction not
/// to combine Registry64 with a manual WOW6432Node traversal.
/// </summary>
public sealed record ComRegistrationSource
{
    public required string Label { get; init; }
    public required RegistryHive Hive { get; init; }
    public required RegistryView View { get; init; }
    public required string RegistrationScope { get; init; } // "Machine" or "User"
    public required string RegistryViewLabel { get; init; } // "Registry64" | "Registry32" | "Default"

    private const string ClsidPath = @"SOFTWARE\Classes\CLSID";

    public static readonly ComRegistrationSource LocalMachine64 = new()
    {
        Label = @"HKLM\SOFTWARE\Classes\CLSID",
        Hive = RegistryHive.LocalMachine,
        View = RegistryView.Registry64,
        RegistrationScope = "Machine",
        RegistryViewLabel = "Registry64"
    };

    public static readonly ComRegistrationSource LocalMachine32 = new()
    {
        Label = @"HKLM\SOFTWARE\WOW6432Node\Classes\CLSID",
        Hive = RegistryHive.LocalMachine,
        View = RegistryView.Registry32,
        RegistrationScope = "Machine",
        RegistryViewLabel = "Registry32"
    };

    public static readonly ComRegistrationSource CurrentUser = new()
    {
        Label = @"HKCU\SOFTWARE\Classes\CLSID",
        Hive = RegistryHive.CurrentUser,
        View = RegistryView.Default,
        RegistrationScope = "User",
        RegistryViewLabel = "Default"
    };

    public static readonly IReadOnlyList<ComRegistrationSource> All = [LocalMachine64, LocalMachine32, CurrentUser];

    public string Path => ClsidPath;
}
