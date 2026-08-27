namespace ServerSleuth.Analysis.Risk.Models;

/// <summary>
/// The aggregate risk picture for a single <see cref="ServerSleuth.Core.Boundaries.ApplicationBoundary"/>
/// — see skill.md (Phase 7B) §4. Built only from findings whose (explicit or derived — see
/// <see cref="Aggregation.ApplicationRiskAggregator"/>) <c>ApplicationBoundaryId</c> equals this
/// boundary's Id. Never invents application ownership: a finding only ever belongs here when
/// its source entity is an actual member of this boundary.
/// </summary>
public sealed record ApplicationRiskSummary : RiskSummaryBase
{
    public required string ApplicationBoundaryId { get; init; }
    public required string ApplicationBoundaryName { get; init; }
}
