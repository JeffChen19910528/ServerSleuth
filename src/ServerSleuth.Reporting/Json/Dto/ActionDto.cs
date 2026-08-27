namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>Mirrors <see cref="ServerSleuth.Analysis.Migration.Actions.MigrationAction"/> — a
/// declarative recommendation only. <c>Description</c>/<c>Rationale</c> are free text produced by
/// Phase 8B's own fixed policy templates (never a shell command, never user/config-supplied text
/// — see <c>MigrationActionPlanner</c>), so no additional sanitization is needed at this
/// boundary beyond carrying the two fields through unchanged.</summary>
public sealed record ActionDto
{
    public required string ActionId { get; init; }
    public required string ActionType { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Priority { get; init; }
    public required string Phase { get; init; }
    public required IReadOnlyList<string> AffectedBoundaryIds { get; init; }
    public required IReadOnlyList<string> AffectedEntityIds { get; init; }
    public required IReadOnlyList<string> RelatedIssueIds { get; init; }
    public required IReadOnlyList<string> RelatedDependencyIds { get; init; }
    public required IReadOnlyList<EvidenceDto> Evidence { get; init; }
    public required string Rationale { get; init; }
}
