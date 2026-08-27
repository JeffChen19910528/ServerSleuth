using System.Globalization;

namespace ServerSleuth.Windows.Software;

/// <summary>
/// Decides whether an Uninstall registry subkey represents real installed software, and maps
/// it into a SoftwareRegistryRow. Pure function over raw values so it is unit-testable without
/// touching the registry. See skill.md §10.
/// </summary>
public static class SoftwareRegistryRowBuilder
{
    public static bool TryBuild(string registryKeyName, IReadOnlyDictionary<string, object?> values, out SoftwareRegistryRow row)
    {
        row = null!;

        if (values.GetValueOrDefault("DisplayName") is not string displayName || string.IsNullOrWhiteSpace(displayName))
        {
            return false; // Most entries without a DisplayName are patches/components, not installed software.
        }

        if (values.GetValueOrDefault("SystemComponent") is int systemComponent && systemComponent == 1)
        {
            return false;
        }

        row = new SoftwareRegistryRow
        {
            RegistryKeyName = registryKeyName,
            DisplayName = displayName,
            DisplayVersion = values.GetValueOrDefault("DisplayVersion") as string,
            Publisher = values.GetValueOrDefault("Publisher") as string,
            InstallLocation = values.GetValueOrDefault("InstallLocation") as string,
            InstallDate = ParseInstallDate(values.GetValueOrDefault("InstallDate") as string),
            UninstallString = values.GetValueOrDefault("UninstallString") as string
        };

        return true;
    }

    private static DateTimeOffset? ParseInstallDate(string? raw)
    {
        if (raw is not null && DateTime.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return new DateTimeOffset(parsed, TimeSpan.Zero);
        }

        return null;
    }
}
