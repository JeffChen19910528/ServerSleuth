namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>Mirrors <see cref="ServerSleuth.Analysis.Migration.Consolidation.MigrationDependencyGroup"/>
/// — dependencies grouped by their existing type, in the exact order Phase 8C already sorted them.</summary>
public sealed record DependencyGroupDto
{
    public required string Type { get; init; }
    public required IReadOnlyList<DependencyDto> Dependencies { get; init; }
}
