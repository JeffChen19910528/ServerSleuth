using ServerSleuth.Infrastructure.Runtimes;
using Microsoft.Win32;
using ServerSleuth.Windows.Registry;

namespace ServerSleuth.Windows.Runtimes.Detectors;

/// <summary>
/// Detects installed .NET Framework via the official Release-value table (Microsoft's own
/// documented detection method) at HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full,
/// plus the standalone v3.5 key. This registry location is not WOW64-redirected — the same
/// key is visible regardless of process bitness — so only one view is queried. See skill.md §6.
/// </summary>
public sealed class DotNetFrameworkDetector(IWindowsRegistryReader registryReader) : IRuntimeDetector
{
    private const string V4FullPath = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full";
    private const string V35Path = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5";

    // Descending Release-value thresholds, per Microsoft's official mapping table.
    private static readonly (int MinRelease, string Version)[] ReleaseTable =
    [
        (533320, "4.8.1"),
        (528040, "4.8"),
        (461808, "4.7.2"),
        (461308, "4.7.1"),
        (460798, "4.7"),
        (394802, "4.6.2"),
        (394254, "4.6.1"),
        (393295, "4.6"),
        (379893, "4.5.2"),
        (378675, "4.5.1"),
        (378389, "4.5")
    ];

    public string Id => "dotnet-framework-detector";
    public string RuntimeFamily => "DotNetFramework";

    public Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken)
    {
        var rows = new List<RuntimeDetectionRow>();

        var v4Values = registryReader.GetValues(RegistryHive.LocalMachine, RegistryView.Registry64, V4FullPath);
        if (v4Values.Success && v4Values.Value is not null)
        {
            var release = v4Values.Value.GetValueOrDefault("Release") is int r ? r : (int?)null;
            var rawVersion = v4Values.Value.GetValueOrDefault("Version") as string;
            var installPath = v4Values.Value.GetValueOrDefault("InstallPath") as string;

            var mappedVersion = release is not null ? MapRelease(release.Value) : null;

            rows.Add(new RuntimeDetectionRow
            {
                Family = RuntimeFamily,
                EntityKind = RuntimeEntityKind.Runtime,
                Name = ".NET Framework",
                Version = mappedVersion ?? rawVersion, // never guessed — falls back to the registry's own raw Version string, not an invented mapping
                InstallationPath = installPath,
                DetectionSources = ["Registry"],
                RegistryPath = $@"HKLM\{V4FullPath}",
                ConflictNote = release is not null && mappedVersion is null
                    ? $"Release value {release} did not match any known threshold — reporting registry's raw Version instead of guessing."
                    : null
            });
        }

        var v35Values = registryReader.GetValues(RegistryHive.LocalMachine, RegistryView.Registry64, V35Path);
        var v35Install = v35Values.Value?.GetValueOrDefault("Install");
        if (v35Values.Success && v35Install is int i && i == 1)
        {
            rows.Add(new RuntimeDetectionRow
            {
                Family = RuntimeFamily,
                EntityKind = RuntimeEntityKind.Runtime,
                Name = ".NET Framework",
                Version = "3.5",
                DetectionSources = ["Registry"],
                RegistryPath = $@"HKLM\{V35Path}"
            });
        }

        return Task.FromResult(rows.Count > 0 ? RuntimeDetectionResult.Detected(rows) : RuntimeDetectionResult.NotDetected());
    }

    internal static string? MapRelease(int release) =>
        ReleaseTable.FirstOrDefault(entry => release >= entry.MinRelease).Version;
}
