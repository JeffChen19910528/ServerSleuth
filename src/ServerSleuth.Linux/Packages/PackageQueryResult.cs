namespace ServerSleuth.Linux.Packages;

public sealed record PackageQueryResult
{
    public required PackageManagerAvailability Status { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<PackageRow> Packages { get; init; } = [];
}
