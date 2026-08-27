namespace ServerSleuth.Analysis.Migration.Consolidation;

/// <summary>
/// Auditable, deterministic record of one <see cref="ServerMigrationAssessmentReportEngine"/> run
/// — mirrors <c>MigrationDiagnostics</c>/<c>MigrationPlanDiagnostics</c>'s philosophy. Purely a
/// tally of what this consolidation pass composed — it never records a decision, since Phase 8C
/// makes none: no severity, status, action, or check is computed here, only assembled from
/// already-produced Phase 7B/8A/8B output.
/// </summary>
public sealed class ConsolidationDiagnostics
{
    public int ApplicationsConsolidated { get; private set; }
    public int ServerLevelIssueCount { get; private set; }
    public int SharedInfrastructureDependencyCount { get; private set; }
    public int CoverageWarningCount { get; private set; }
    public int GraphValidationErrorCount { get; private set; }

    public void RecordApplicationsConsolidated(int count) => ApplicationsConsolidated = count;
    public void RecordServerLevelIssues(int count) => ServerLevelIssueCount = count;
    public void RecordSharedInfrastructureDependencies(int count) => SharedInfrastructureDependencyCount = count;
    public void RecordCoverageWarnings(int count) => CoverageWarningCount = count;
    public void RecordGraphValidationErrors(int count) => GraphValidationErrorCount = count;
}
