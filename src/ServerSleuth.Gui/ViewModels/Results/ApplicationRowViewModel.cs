using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Models;
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
