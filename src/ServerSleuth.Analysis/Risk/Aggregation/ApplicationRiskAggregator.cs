using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Boundaries;

namespace ServerSleuth.Analysis.Risk.Aggregation;

/// <summary>
/// Groups RiskFindings by ApplicationBoundary and builds one <see cref="ApplicationRiskSummary"/>
/// per boundary that has at least one attributed finding — see skill.md (Phase 7B) §4. A
/// boundary with zero findings produces no summary at all, rather than an empty/None one, so
/// <c>ApplicationSummaries</c> only ever lists applications that actually have something to
/// report.
/// </summary>
internal static class ApplicationRiskAggregator
{
    /// <param name="findingsByBoundaryId">Already-grouped findings — see
    /// <see cref="RiskAggregator.ResolveBoundaryId"/> for how a finding is attributed to a
    /// boundary Id (its own explicit <c>ApplicationBoundaryId</c>, falling back to the entity's
    /// boundary membership already computed by Phase 5B).</param>
    public static IReadOnlyList<ApplicationRiskSummary> Build(
        IReadOnlyDictionary<string, ApplicationBoundary> boundariesById,
        IReadOnlyDictionary<string, List<RiskFinding>> findingsByBoundaryId)
    {
        var summaries = new List<ApplicationRiskSummary>();

        foreach (var boundaryId in findingsByBoundaryId.Keys.OrderBy(id => id, StringComparer.Ordinal))
        {
            var findings = findingsByBoundaryId[boundaryId];
            if (findings.Count == 0)
            {
                continue;
            }

            var boundaryName = boundariesById.TryGetValue(boundaryId, out var boundary) ? boundary.Name : boundaryId;

            summaries.Add(new ApplicationRiskSummary
            {
                ApplicationBoundaryId = boundaryId,
                ApplicationBoundaryName = boundaryName,
                OverallSeverity = RiskSummaryCalculator.ComputeOverallSeverity(findings),
                CriticalCount = RiskSummaryCalculator.CountBySeverity(findings, RiskSeverity.Critical),
                HighCount = RiskSummaryCalculator.CountBySeverity(findings, RiskSeverity.High),
                MediumCount = RiskSummaryCalculator.CountBySeverity(findings, RiskSeverity.Medium),
                LowCount = RiskSummaryCalculator.CountBySeverity(findings, RiskSeverity.Low),
                InfoCount = RiskSummaryCalculator.CountBySeverity(findings, RiskSeverity.Info),
                TotalFindingCount = findings.Count,
                AffectedEntityCount = RiskSummaryCalculator.ComputeAffectedEntityCount(findings),
                AffectedBoundaryCount = 1,
                Findings = findings,
                TopRisks = RiskSummaryCalculator.ComputeTopRisks(findings),
                CategoryCounts = RiskSummaryCalculator.ComputeCategoryCounts(findings),
                SharedDependencyCount = RiskSummaryCalculator.ComputeSharedDependencyCount(findings),
                AggregateConfidence = RiskSummaryCalculator.ComputeAggregateConfidence(findings)
            });
        }

        return summaries;
    }
}
