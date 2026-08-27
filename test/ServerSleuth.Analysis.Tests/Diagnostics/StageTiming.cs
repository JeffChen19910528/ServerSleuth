namespace ServerSleuth.Analysis.Tests.Diagnostics;

/// <summary>
/// One pipeline stage's diagnostic timing/size snapshot — see skill.md (Phase 10A-H) §3.
/// Deliberately carries only aggregate counts/durations, never raw entity/configuration content
/// or secrets (§15).
/// </summary>
internal sealed record StageTiming
{
    public required string StageName { get; init; }
    public required double DurationMilliseconds { get; init; }
    public int? InputEntityCount { get; init; }
    public int? InputEdgeCount { get; init; }
    public int? OutputEntityCount { get; init; }
    public int? OutputEdgeCount { get; init; }
    public int? FindingCount { get; init; }
    public int? BoundaryCount { get; init; }
    public int? DependencyCount { get; init; }
    public bool TimedOut { get; init; }

    public string FormatRow()
    {
        var time = TimedOut ? $">{DurationMilliseconds:0}ms (ABORTED)" : $"{DurationMilliseconds:0.0}ms";
        var parts = new List<string> { $"{StageName,-22} {time,18}" };
        if (InputEntityCount is { } ie) parts.Add($"in-entities={ie}");
        if (InputEdgeCount is { } iedg) parts.Add($"in-edges={iedg}");
        if (OutputEntityCount is { } oe) parts.Add($"out-entities={oe}");
        if (OutputEdgeCount is { } oedg) parts.Add($"out-edges={oedg}");
        if (FindingCount is { } fc) parts.Add($"findings={fc}");
        if (BoundaryCount is { } bc) parts.Add($"boundaries={bc}");
        if (DependencyCount is { } dc) parts.Add($"dependencies={dc}");
        return string.Join("  ", parts);
    }
}
