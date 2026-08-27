namespace ServerSleuth.Windows.IIS;

public sealed record IisSnapshot
{
    public IReadOnlyList<IisSiteRow> Sites { get; init; } = [];
    public IReadOnlyList<IisAppPoolRow> ApplicationPools { get; init; } = [];
}
