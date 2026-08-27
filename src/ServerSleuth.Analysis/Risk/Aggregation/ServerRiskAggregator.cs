using ServerSleuth.Analysis.Risk.Models;

namespace ServerSleuth.Analysis.Risk.Aggregation;

/// <summary>
/// Builds the whole-server <see cref="ServerRiskSummary"/> over EVERY finding — application-
/// scoped and server-scoped alike — plus the per-application breakdown. See skill.md
/// (Phase 7B) §5, §13: a finding is never lost merely because it isn't attributed to any
/// application boundary.
/// </summary>
internal static class ServerRiskAggregator
{
    public static ServerRiskSummary Build(
        IReadOnlyList<RiskFinding> allFindings,
        IReadOnlyList<ApplicationRiskSummary> applicationSummaries,
        int serverScopedFindingCount)
    {
        return new ServerRiskSummary
        {
            ApplicationSummaries = applicationSummaries,
            ServerScopedFindingCount = serverScopedFindingCount,
            OverallSeverity = RiskSummaryCalculator.ComputeOverallSeverity(allFindings),
            CriticalCount = RiskSummaryCalculator.CountBySeverity(allFindings, RiskSeverity.Critical),
            HighCount = RiskSummaryCalculator.CountBySeverity(allFindings, RiskSeverity.High),
            MediumCount = RiskSummaryCalculator.CountBySeverity(allFindings, RiskSeverity.Medium),
            LowCount = RiskSummaryCalculator.CountBySeverity(allFindings, RiskSeverity.Low),
            InfoCount = RiskSummaryCalculator.CountBySeverity(allFindings, RiskSeverity.Info),
            TotalFindingCount = allFindings.Count,
            AffectedEntityCount = RiskSummaryCalculator.ComputeAffectedEntityCount(allFindings),
            AffectedBoundaryCount = applicationSummaries.Count,
            Findings = allFindings,
            TopRisks = RiskSummaryCalculator.ComputeTopRisks(allFindings),
            CategoryCounts = RiskSummaryCalculator.ComputeCategoryCounts(allFindings),
            SharedDependencyCount = RiskSummaryCalculator.ComputeSharedDependencyCount(allFindings),
            AggregateConfidence = RiskSummaryCalculator.ComputeAggregateConfidence(allFindings)
        };
    }
}
