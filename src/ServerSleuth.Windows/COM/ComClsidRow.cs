namespace ServerSleuth.Windows.COM;

public sealed record ComClsidRow
{
    public required string Clsid { get; init; }
    public string? Name { get; init; }
    public string? ProgId { get; init; }
    public ServerReference? InprocServer32 { get; init; }
    public string? ThreadingModel { get; init; }
    public ServerReference? LocalServer32 { get; init; }
    public string? TypeLibClsid { get; init; }
    public string? VersionValue { get; init; }
}
