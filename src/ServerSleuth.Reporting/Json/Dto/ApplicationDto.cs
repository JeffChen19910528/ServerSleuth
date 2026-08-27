namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>Mirrors <see cref="ServerSleuth.Analysis.Migration.Consolidation.ApplicationMigrationSummary"/>
/// — one entry per <c>ApplicationBoundary</c> Phase 8A already included.</summary>
public sealed record ApplicationDto
{
    public required string BoundaryId { get; init; }
    public required string ApplicationName { get; init; }
    public required string MigrationStatus { get; init; }
    public required string RiskSeverity { get; init; }
    public required int AffectedEntityCount { get; init; }
    public required int AffectedBoundaryCount { get; init; }
    public required IReadOnlyList<IssueDto> Issues { get; init; }
    public required IReadOnlyList<DependencyDto> Dependencies { get; init; }
    public required IReadOnlyList<ActionDto> Actions { get; init; }
    public required IReadOnlyList<CheckDto> PreMigrationChecks { get; init; }
    public required IReadOnlyList<CheckDto> PostMigrationChecks { get; init; }
}
