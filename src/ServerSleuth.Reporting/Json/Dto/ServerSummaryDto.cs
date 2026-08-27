namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>Mirrors <see cref="ServerSleuth.Analysis.Migration.Consolidation.ServerMigrationSummary"/>
/// — every count copied verbatim, no numeric migration-readiness score anywhere.</summary>
public sealed record ServerSummaryDto
{
    public required string OverallMigrationStatus { get; init; }
    public required string OverallRiskSeverity { get; init; }
    public required int ApplicationCount { get; init; }
    public required int BlockedApplicationCount { get; init; }
    public required int NeedsRemediationApplicationCount { get; init; }
    public required int ReadyWithConditionsApplicationCount { get; init; }
    public required int ReadyApplicationCount { get; init; }
    public required int BlockingIssueCount { get; init; }
    public required int RemediationIssueCount { get; init; }
    public required int ConditionalDependencyCount { get; init; }
    public required int ActionCount { get; init; }
    public required int VerificationCheckCount { get; init; }
    public required int DependencyCount { get; init; }
    public required int AffectedEntityCount { get; init; }
    public required int AffectedBoundaryCount { get; init; }
}
