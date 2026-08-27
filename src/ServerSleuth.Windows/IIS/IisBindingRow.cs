namespace ServerSleuth.Windows.IIS;

public sealed record IisBindingRow
{
    public required string Protocol { get; init; }
    public required string IpAddress { get; init; }
    public required int Port { get; init; }
    public string? HostName { get; init; }
    public required string BindingInformation { get; init; }
    public string? CertificateThumbprint { get; init; }
    public string? CertificateStoreName { get; init; }
}
