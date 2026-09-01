using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Verification;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Gui.ViewModels.Results;

/// <summary>
/// GUI-4 §Step7: one application's detail panel — the "thin GUI presentation model" the phase
/// spec allows, built by <see cref="ApplicationRowViewModel"/> purely by JOINING two
/// already-computed, already-consolidated records that Phase 8C/7B produced:
/// <see cref="ApplicationMigrationSummary"/> (identity/migration/actions/checks — everything the
/// report itself already scopes to this one application) and, when the matching boundary also
/// has one, its <see cref="ApplicationRiskSummary"/> (confidence/top risks/finding counts — the
/// report's own <c>ApplicationMigrationSummary.RiskSeverity</c> is only the single aggregate
/// value, not the full risk picture, so the underlying <see cref="RiskAggregationResult"/> is
/// still needed here for the finding-level detail).
///
/// Every list exposed below is the EXACT SAME <c>IReadOnlyList</c> instance already sitting on
/// the source records — never copied, filtered-and-reallocated, or re-sorted. No property here
/// performs any calculation beyond simple counts already available as <c>.Count</c>.
/// </summary>
public sealed class ApplicationDetailViewModel
{
    public ApplicationDetailViewModel(ApplicationMigrationSummary migration, ApplicationRiskSummary? risk,
        ApplicationComponentsViewModel? components = null)
    {
        Migration = migration;
        Risk = risk;
        Components = components ?? new ApplicationComponentsViewModel([], []);
    }

    /// <summary>Phase 8C's own consolidated per-application view — never re-derived.</summary>
    public ApplicationMigrationSummary Migration { get; }

    /// <summary>Null only when Risk Aggregation produced no <see cref="ApplicationRiskSummary"/>
    /// for this boundary at all (skill.md (Phase 7B) §4: a boundary with zero attributed
    /// findings never gets one) — the panel must still render the migration side in that case,
    /// per GUI-4 §19's "no application findings" empty-state requirement.</summary>
    public ApplicationRiskSummary? Risk { get; }

    /// <summary>GUI-8B: entity components discovered for this application through its boundary
    /// membership — what exists on the current server that must be prepared on the target.
    /// Never null; defaults to an empty components set when no pipeline data is available.</summary>
    public ApplicationComponentsViewModel Components { get; }

    // ----- IDENTITY -----
    public string ApplicationName => Migration.Assessment.ApplicationBoundaryName;
    public string ApplicationBoundaryId => Migration.Assessment.ApplicationBoundaryId;

    // ----- RISK -----
    public AggregateSeverity RiskSeverity => Migration.RiskSeverity;
    public Confidence AggregateConfidence => Risk?.AggregateConfidence ?? new Confidence(0.0);
    public int FindingCount => Risk?.TotalFindingCount ?? 0;

    /// <summary>Phase 7B's own deterministically-ordered subset (Severity desc, Impact desc,
    /// Confidence desc, RuleId/FindingId ordinal) — never re-ranked here.</summary>
    public IReadOnlyList<RiskFinding> TopRisks => Risk?.TopRisks ?? [];

    public IReadOnlyList<RiskFinding> AllFindings => Risk?.Findings ?? [];

    // ----- MIGRATION -----
    public MigrationStatus MigrationStatus => Migration.Assessment.OverallStatus;
    public IReadOnlyList<MigrationIssue> Issues => Migration.Assessment.Issues;
    public IReadOnlyList<MigrationAction> Actions => Migration.Actions;
    public IReadOnlyList<MigrationVerificationCheck> PreMigrationChecks => Migration.PreMigrationChecks;
    public IReadOnlyList<MigrationVerificationCheck> PostMigrationChecks => Migration.PostMigrationChecks;

    // ----- DEPENDENCIES -----
    public IReadOnlyList<MigrationDependency> Dependencies => Migration.Assessment.Dependencies;

    public int AffectedEntityCount => Migration.Assessment.AffectedEntityCount;
    public int AffectedBoundaryCount => Migration.Assessment.AffectedBoundaryCount;
}
