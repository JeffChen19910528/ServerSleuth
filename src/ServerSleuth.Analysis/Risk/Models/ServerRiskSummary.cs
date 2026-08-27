namespace ServerSleuth.Analysis.Risk.Models;

/// <summary>
/// The whole-server aggregate — see skill.md (Phase 7B) §5. Its <c>Findings</c>/counts/metrics
/// cover EVERY finding from the Phase 7A <c>RiskAnalysisResult</c>, application-scoped and
/// server-scoped alike: a finding is never dropped merely because it has no
/// <c>ApplicationBoundaryId</c>. <c>ApplicationSummaries</c> gives the per-application
/// breakdown; <c>ServerScopedFindingCount</c> reports how many findings in the total belong to
/// no application boundary at all (platform-level AccessDenied, graph integrity, unresolved
/// infrastructure, etc. — see skill.md (Phase 7B) §13).
/// </summary>
public sealed record ServerRiskSummary : RiskSummaryBase
{
    public required IReadOnlyList<ApplicationRiskSummary> ApplicationSummaries { get; init; }

    public required int ServerScopedFindingCount { get; init; }
}
