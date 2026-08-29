using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Gui.ViewModels.Results;

/// <summary>
/// GUI-4 §Step6: one row in the Applications table. Wraps a single
/// <see cref="ApplicationMigrationSummary"/> (already ordinal-sorted by BoundaryId in
/// <c>ServerMigrationAssessmentReport.ApplicationAssessments</c> — this type never re-sorts, it
/// only projects) plus the matching <see cref="ApplicationRiskSummary"/>, joined once, eagerly,
/// at construction time — never re-joined or recomputed on selection/filtering/tab-switching.
/// </summary>
public sealed class ApplicationRowViewModel
{
    public ApplicationRowViewModel(ApplicationMigrationSummary migration, ApplicationRiskSummary? risk)
    {
        Detail = new ApplicationDetailViewModel(migration, risk);
    }

    /// <summary>GUI-7B: the exact join <see cref="Results.ResultsDashboardViewModel"/>'s own
    /// constructor already performed, extracted so <c>MigrationOverviewViewModel</c> can build
    /// the identical row list from the same <see cref="ScanPipelineResult"/> without duplicating
    /// the join logic (never a second migration/risk recomputation — both callers read the exact
    /// same already-consolidated <c>ApplicationMigrationSummary</c>/<c>ApplicationRiskSummary</c>
    /// records).</summary>
    public static IReadOnlyList<ApplicationRowViewModel> BuildFrom(ScanPipelineResult? pipeline)
    {
        var report = pipeline?.Report;
        var serverRisk = pipeline?.Aggregation.Server;
        var riskByBoundary = serverRisk?.ApplicationSummaries.ToDictionary(a => a.ApplicationBoundaryId, StringComparer.Ordinal)
            ?? new Dictionary<string, ApplicationRiskSummary>(StringComparer.Ordinal);

        // Preserves ServerMigrationAssessmentReport.ApplicationAssessments' own ordinal-by-
        // BoundaryId ordering — never sorted here.
        return report?.ApplicationAssessments
            .Select(a => new ApplicationRowViewModel(a, riskByBoundary.GetValueOrDefault(a.Assessment.ApplicationBoundaryId)))
            .ToList()
            ?? [];
    }

    /// <summary>The reusable detail panel for this row — built once, shown whenever this row is
    /// selected (GUI-4 §Step6: "selecting an application must open an application-detail
    /// view/panel").</summary>
    public ApplicationDetailViewModel Detail { get; }

    public string ApplicationName => Detail.ApplicationName;
    public string ApplicationBoundaryId => Detail.ApplicationBoundaryId;
    public MigrationStatus MigrationStatus => Detail.MigrationStatus;
    public AggregateSeverity RiskSeverity => Detail.RiskSeverity;
    public Confidence Confidence => Detail.AggregateConfidence;
    public int IssueCount => Detail.Issues.Count;
    public int DependencyCount => Detail.Dependencies.Count;
    public int AffectedEntityCount => Detail.AffectedEntityCount;
}
