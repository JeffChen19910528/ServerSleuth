namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>
/// The root JSON contract — see skill.md (Phase 9A) §5. Every field maps 1:1 to something
/// <see cref="ServerSleuth.Analysis.Migration.Consolidation.ServerMigrationAssessmentReport"/>
/// (Phase 8C) already produced; nothing here is computed, only reshaped for serialization. Field
/// order below is JSON property order (System.Text.Json serializes in declaration order), and
/// every collection is already ordinal-sorted by the source Phase 8C model — this DTO never
/// re-sorts or re-derives ordering itself.
/// </summary>
public sealed record ServerReportDto
{
    public required ServerSummaryDto Server { get; init; }
    public required string Coverage { get; init; }
    public required IReadOnlyList<CoverageWarningDto> CoverageWarnings { get; init; }
    public required IReadOnlyList<ApplicationDto> Applications { get; init; }
    public required IReadOnlyList<IssueDto> ServerLevelIssues { get; init; }
    public required IReadOnlyList<DependencyDto> SharedInfrastructure { get; init; }
    public required IReadOnlyList<DependencyGroupDto> Dependencies { get; init; }
    public required IReadOnlyList<ActionDto> Actions { get; init; }
    public required IReadOnlyList<CheckDto> PreMigrationChecks { get; init; }
    public required IReadOnlyList<CheckDto> PostMigrationChecks { get; init; }
    public required IReadOnlyList<GraphValidationFindingDto> GraphValidationErrors { get; init; }
    public required DiagnosticsDto Diagnostics { get; init; }
}
