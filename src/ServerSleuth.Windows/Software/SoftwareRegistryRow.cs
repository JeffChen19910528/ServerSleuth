namespace ServerSleuth.Windows.Software;

public sealed record SoftwareRegistryRow
{
    public required string RegistryKeyName { get; init; }
    public required string DisplayName { get; init; }
    public string? DisplayVersion { get; init; }
    public string? Publisher { get; init; }
    public string? InstallLocation { get; init; }
    public DateTimeOffset? InstallDate { get; init; }
    public string? UninstallString { get; init; }
}
