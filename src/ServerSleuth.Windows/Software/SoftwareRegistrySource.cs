using Microsoft.Win32;
using ServerSleuth.Core.Enums;

namespace ServerSleuth.Windows.Software;

/// <summary>The three Uninstall registry locations named in skill.md §10.</summary>
public sealed record SoftwareRegistrySource
{
    public required string Label { get; init; }
    public required RegistryHive Hive { get; init; }
    public required RegistryView View { get; init; }
    public required EntityArchitecture ArchitectureHint { get; init; }

    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public static readonly SoftwareRegistrySource LocalMachine64 = new()
    {
        Label = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        Hive = RegistryHive.LocalMachine,
        View = RegistryView.Registry64,
        ArchitectureHint = EntityArchitecture.X64
    };

    public static readonly SoftwareRegistrySource LocalMachine32 = new()
    {
        Label = @"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        Hive = RegistryHive.LocalMachine,
        View = RegistryView.Registry32,
        ArchitectureHint = EntityArchitecture.X86
    };

    public static readonly SoftwareRegistrySource CurrentUser = new()
    {
        Label = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        Hive = RegistryHive.CurrentUser,
        View = RegistryView.Default,
        ArchitectureHint = EntityArchitecture.Unknown
    };

    public static readonly IReadOnlyList<SoftwareRegistrySource> All = [LocalMachine64, LocalMachine32, CurrentUser];

    public string Path => UninstallPath;
}
