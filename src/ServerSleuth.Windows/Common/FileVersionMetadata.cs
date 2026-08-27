namespace ServerSleuth.Windows.Common;

public sealed record FileVersionMetadata
{
    public string? FileVersion { get; init; }
    public string? ProductVersion { get; init; }
    public string? CompanyName { get; init; }
    public string? ProductName { get; init; }
}
