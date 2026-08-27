namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>Mirrors <see cref="ServerSleuth.Analysis.Migration.Consolidation.ConsolidationDiagnostics"/>
/// — auditable counts only, never a decision.</summary>
public sealed record DiagnosticsDto
{
    public required int ApplicationsConsolidated { get; init; }
    public required int ServerLevelIssueCount { get; init; }
    public required int SharedInfrastructureDependencyCount { get; init; }
    public required int CoverageWarningCount { get; init; }
    public required int GraphValidationErrorCount { get; init; }
}
