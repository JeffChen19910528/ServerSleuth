namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>Mirrors <see cref="ServerSleuth.Analysis.Migration.Models.MigrationDependency"/> —
/// see skill.md (Phase 9A) §5, §7. <c>Target</c> is already a normalized identifier/endpoint the
/// discovery layer produced (e.g. a host:port or file-share path), never a raw configuration
/// blob or credential — that distinction is enforced upstream (Phase 8A never carries connection-
/// string credentials into <c>MigrationDependency.Target</c>), and this DTO carries it through
/// unchanged, never widened to include anything else from the source entity.</summary>
public sealed record DependencyDto
{
    public required string DependencyId { get; init; }
    public required string Type { get; init; }
    public required string Target { get; init; }
    public required IReadOnlyList<string> AffectedBoundaryIds { get; init; }
    public required ConfidenceDto Confidence { get; init; }
    public required IReadOnlyList<EvidenceDto> Evidence { get; init; }
    public required string VerificationPhase { get; init; }
    public required string VerificationRequirement { get; init; }
    public string? RelatedRiskFindingId { get; init; }
}
