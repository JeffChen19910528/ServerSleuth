namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>Mirrors <see cref="ServerSleuth.Analysis.Migration.Models.MigrationIssue"/> field for
/// field — see skill.md (Phase 9A) §5, §8: full provenance (RuleId/SourceRiskFindingId/Evidence/
/// Confidence) is preserved, nothing is recalculated or renamed.</summary>
public sealed record IssueDto
{
    public required string IssueId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Severity { get; init; }
    public required string MigrationStatusImpact { get; init; }
    public required string RuleId { get; init; }
    public required string SourceRiskFindingId { get; init; }
    public required IReadOnlyList<string> AffectedBoundaryIds { get; init; }
    public required IReadOnlyList<string> AffectedEntityIds { get; init; }
    public required IReadOnlyList<EvidenceDto> Evidence { get; init; }
    public required ConfidenceDto Confidence { get; init; }
    public required string RequiredAction { get; init; }
    public required string PolicyDecisionReason { get; init; }
}
