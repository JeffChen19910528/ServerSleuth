namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>Mirrors <see cref="ServerSleuth.Analysis.Migration.Verification.MigrationVerificationCheck"/>
/// — a declarative checklist item; rendering never performs the check itself.</summary>
public sealed record CheckDto
{
    public required string CheckId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Phase { get; init; }
    public required string CheckType { get; init; }
    public required IReadOnlyList<string> AffectedBoundaryIds { get; init; }
    public required IReadOnlyList<string> RelatedActionIds { get; init; }
    public required IReadOnlyList<string> RelatedDependencyIds { get; init; }
    public required IReadOnlyList<EvidenceDto> Evidence { get; init; }
    public required string Rationale { get; init; }
}
