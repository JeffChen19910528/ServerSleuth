namespace ServerSleuth.Windows.IIS;

public sealed record IisSiteRow
{
    public required string Name { get; init; }
    public required long SiteId { get; init; }
    public required string State { get; init; }
    public string? PhysicalPath { get; init; }
    public IReadOnlyList<IisBindingRow> Bindings { get; init; } = [];
    public IReadOnlyList<IisApplicationRow> Applications { get; init; } = [];
}
