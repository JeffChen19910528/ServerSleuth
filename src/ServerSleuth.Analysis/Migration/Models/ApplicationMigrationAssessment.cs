namespace ServerSleuth.Analysis.Migration.Models;

/// <summary>
/// The migration picture for one <see cref="ServerSleuth.Core.Boundaries.ApplicationBoundary"/>
/// — see skill.md (Phase 8A) §8. Built only from boundaries that actually have at least one
/// attributed RiskFinding (mirroring <c>ApplicationRiskSummary</c>'s own rule — see
/// <see cref="ServerSleuth.Analysis.Risk.Aggregation.ApplicationRiskAggregator"/>); never a
/// synthetic boundary, never an invented ownership relationship.
/// </summary>
public sealed record ApplicationMigrationAssessment : MigrationAssessmentBase
{
    public required string ApplicationBoundaryId { get; init; }
    public required string ApplicationBoundaryName { get; init; }
}
