using ServerSleuth.Analysis.Risk.Diagnostics;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Boundaries;

namespace ServerSleuth.Analysis.Risk.Aggregation;

/// <summary>
/// Phase 7B entry point — transforms a Phase 7A <see cref="RiskAnalysisResult"/> into a
/// deterministic <see cref="RiskAggregationResult"/>. See skill.md (Phase 7B) §1.
///
/// Pure in-memory consumer of already-produced artifacts: never re-runs discovery, never
/// re-evaluates rules, never touches the filesystem/registry/process API/network/systemd/
/// Docker/Podman/Kubernetes. Never mutates its inputs (<c>RiskAnalysisResult.Findings</c>,
/// the <see cref="RiskAnalysisContext"/>'s graph/boundaries) — every summary stores references
/// to the exact same RiskFinding instances Phase 7A produced, never copies or re-identifies
/// them, and never re-deduplicates (Phase 7A's <c>RiskRuleEngine</c> already did that — see
/// skill.md (Phase 7B) §7).
/// </summary>
public sealed class RiskAggregator
{
    public RiskAggregationResult Aggregate(RiskAnalysisContext context, RiskAnalysisResult analysisResult)
    {
        var diagnostics = new RiskAggregationDiagnostics();
        var findings = analysisResult.Findings;
        diagnostics.RecordFindingsProcessed(findings.Count);
        diagnostics.RecordFindingsDeduplicatedUpstream(analysisResult.Diagnostics.FindingsDeduplicated);

        var boundariesById = context.Boundaries.ToDictionary(b => b.Id, b => b);
        var byBoundary = new Dictionary<string, List<RiskFinding>>(StringComparer.Ordinal);
        var serverScoped = new List<RiskFinding>();

        foreach (var finding in findings)
        {
            var boundaryIds = ResolveAffectedBoundaryIds(context, finding);
            if (boundaryIds.Count == 0)
            {
                serverScoped.Add(finding);
                continue;
            }

            // A shared dependency affects every one of its boundaries at once — the same
            // logical finding is added to each affected boundary's own view. This is NOT double
            // counting: ServerRiskSummary is built from `findings` (the original, still-
            // deduplicated list) directly, so the logical finding is counted exactly once there
            // regardless of how many boundaries it's attributed to here. See skill.md
            // (Shared Infrastructure Attribution Hardening) §6.
            foreach (var boundaryId in boundaryIds)
            {
                if (!byBoundary.TryGetValue(boundaryId, out var list))
                {
                    list = [];
                    byBoundary[boundaryId] = list;
                }

                list.Add(finding);
            }
        }

        var applicationSummaries = ApplicationRiskAggregator.Build(boundariesById, byBoundary);
        diagnostics.RecordApplicationSummariesCreated(applicationSummaries.Count);
        diagnostics.RecordServerLevelFindingCount(serverScoped.Count);
        diagnostics.RecordUnresolvedOwnershipCount(serverScoped.Count);

        var server = ServerRiskAggregator.Build(findings, applicationSummaries, serverScoped.Count);
        diagnostics.RecordTopRisksSelected(server.TopRisks.Count);

        return new RiskAggregationResult { Server = server, Diagnostics = diagnostics };
    }

    /// <summary>
    /// Every ApplicationBoundary a finding affects — ordinal-sorted, deduplicated, never just
    /// "the first one encountered." See the corrective "Shared Infrastructure Attribution
    /// Hardening" task: a dependency shared by three-or-more workloads is legitimately a member
    /// of all three of their (deliberately un-merged, per Phase 5B §8) boundaries at once, and
    /// all three must remain visible — not silently collapsed onto whichever boundary happened
    /// to be enumerated first.
    ///
    /// Resolution sources, unioned:
    ///   (a) an explicit <c>ApplicationBoundaryId</c> the rule itself stamped (only a few
    ///       Phase 7A rules do this today, always for a genuinely single-owned entity type —
    ///       Certificate/COM/Configuration/ExternalDependency);
    ///   (b) every boundary <see cref="RiskAnalysisContext.BoundaryIdsByEntityId"/> already
    ///       records for the finding's <c>SourceEntityId</c>;
    ///   (c) every boundary <c>BoundaryIdsByEntityId</c> records for each of the finding's
    ///       <c>RelatedEntityIds</c> — needed because a shared-infrastructure finding's
    ///       SourceEntityId is the shared binary itself, while its RelatedEntityIds are the
    ///       sharing workload anchors; unioning both sides is what makes attribution correct
    ///       regardless of which entity a future rule chooses as "the" source.
    ///
    /// Never invents ownership from naming similarity — every Id here traces back to explicit
    /// graph/boundary-membership evidence already computed by Phase 5B. Returns an empty list
    /// (server-scoped, never dropped) when nothing resolves.
    /// </summary>
    internal static IReadOnlyList<string> ResolveAffectedBoundaryIds(RiskAnalysisContext context, RiskFinding finding)
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);

        if (finding.ApplicationBoundaryId is not null)
        {
            ids.Add(finding.ApplicationBoundaryId);
        }

        AddBoundariesFor(finding.SourceEntityId, context, ids);
        foreach (var relatedId in finding.RelatedEntityIds)
        {
            AddBoundariesFor(relatedId, context, ids);
        }

        return ids.ToList();
    }

    private static void AddBoundariesFor(string entityId, RiskAnalysisContext context, SortedSet<string> ids)
    {
        if (context.BoundaryIdsByEntityId.TryGetValue(entityId, out var boundaryIds))
        {
            foreach (var boundaryId in boundaryIds)
            {
                ids.Add(boundaryId);
            }
        }
    }
}
