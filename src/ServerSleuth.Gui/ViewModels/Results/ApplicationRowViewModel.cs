using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

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
    public ApplicationRowViewModel(ApplicationMigrationSummary migration, ApplicationRiskSummary? risk,
        ApplicationComponentsViewModel? components = null)
    {
        Detail = new ApplicationDetailViewModel(migration, risk, components);
    }

    /// <summary>GUI-7B: the exact join <see cref="Results.ResultsDashboardViewModel"/>'s own
    /// constructor already performed, extracted so <c>MigrationOverviewViewModel</c> can build
    /// the identical row list from the same <see cref="ScanPipelineResult"/> without duplicating
    /// the join logic (never a second migration/risk recomputation — both callers read the exact
    /// same already-consolidated <c>ApplicationMigrationSummary</c>/<c>ApplicationRiskSummary</c>
    /// records).
    ///
    /// GUI-8B: also resolves each application's discovered entity components through
    /// <see cref="Core.Boundaries.ApplicationBoundary.MemberEntityIds"/> — no new scan, no new
    /// analysis engine call; the entity index is built once O(N) here and looked up O(1) per
    /// application.</summary>
    public static IReadOnlyList<ApplicationRowViewModel> BuildFrom(ScanPipelineResult? pipeline)
    {
        var report = pipeline?.Report;
        var serverRisk = pipeline?.Aggregation.Server;
        var riskByBoundary = serverRisk?.ApplicationSummaries.ToDictionary(a => a.ApplicationBoundaryId, StringComparer.Ordinal)
            ?? new Dictionary<string, ApplicationRiskSummary>(StringComparer.Ordinal);

        var entityIndex = BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline?.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal)
            ?? new Dictionary<string, ApplicationBoundary>(StringComparer.Ordinal);

        // Preserves ServerMigrationAssessmentReport.ApplicationAssessments' own ordinal-by-
        // BoundaryId ordering — never sorted here.
        return report?.ApplicationAssessments
            .Select(a =>
            {
                var components = ResolveComponents(a.Assessment.ApplicationBoundaryId, boundaryIndex, entityIndex);
                return new ApplicationRowViewModel(
                    a,
                    riskByBoundary.GetValueOrDefault(a.Assessment.ApplicationBoundaryId),
                    components);
            })
            .ToList()
            ?? [];
    }

    /// <summary>Builds an entity-id → entity index from <see cref="ScanPipelineResult.Discovery"/>
    /// and <see cref="ScanPipelineResult.ExternalDependencies"/> exactly once — O(N). Discovery
    /// entities take priority when an id appears in both lists (should not happen in practice).</summary>
    internal static Dictionary<string, DiscoveryEntity> BuildEntityIndex(ScanPipelineResult? pipeline)
    {
        var index = new Dictionary<string, DiscoveryEntity>(StringComparer.Ordinal);
        if (pipeline is null) return index;

        foreach (var ext in pipeline.ExternalDependencies)
        {
            index[ext.Id] = ext;
        }
        foreach (var entity in pipeline.Discovery.Entities)
        {
            index[entity.Id] = entity;
        }
        return index;
    }

    /// <summary>Resolves the member entities for one application boundary through its
    /// <see cref="ApplicationBoundary.MemberEntityIds"/> list. Entity IDs that are not found in
    /// the index are silently skipped — they may belong to a partial scan or a different run.
    /// <see cref="ExternalDependency"/> instances in the index are separated into their own list
    /// so the UI can present them as External Connections rather than generic entities.</summary>
    internal static ApplicationComponentsViewModel ResolveComponents(
        string boundaryId,
        Dictionary<string, ApplicationBoundary> boundaryIndex,
        Dictionary<string, DiscoveryEntity> entityIndex)
    {
        if (!boundaryIndex.TryGetValue(boundaryId, out var boundary))
        {
            return new ApplicationComponentsViewModel([], []);
        }

        var memberEntities = new List<DiscoveryEntity>();
        var externalConnections = new List<ExternalDependency>();

        foreach (var id in boundary.MemberEntityIds)
        {
            if (!entityIndex.TryGetValue(id, out var entity)) continue;

            if (entity is ExternalDependency ext)
            {
                externalConnections.Add(ext);
            }
            else
            {
                memberEntities.Add(entity);
            }
        }

        return new ApplicationComponentsViewModel(memberEntities, externalConnections);
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
