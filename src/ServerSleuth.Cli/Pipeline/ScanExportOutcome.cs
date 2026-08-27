namespace ServerSleuth.Cli.Pipeline;

/// <summary>The result of writing the requested report format(s) to disk.</summary>
public sealed record ScanExportOutcome
{
    public required bool Success { get; init; }
    public required IReadOnlyList<string> WrittenFileNames { get; init; }
    public required IReadOnlyList<string> Diagnostics { get; init; }
}
