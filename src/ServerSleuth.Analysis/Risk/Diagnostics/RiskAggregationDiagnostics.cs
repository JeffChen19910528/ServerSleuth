namespace ServerSleuth.Analysis.Risk.Diagnostics;

public sealed record AggregationIssue
{
    public required string Stage { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// Auditable, deterministic record of one Risk Aggregation run — see skill.md (Phase 7B) §16.
/// Mirrors <see cref="RiskDiagnostics"/>'s philosophy: nothing about aggregation ever happens
/// silently.
/// </summary>
public sealed class RiskAggregationDiagnostics
{
    private readonly List<AggregationIssue> _issues = [];

    public int FindingsProcessed { get; private set; }

    /// <summary>Carried forward from the Phase 7A <c>RiskAnalysisResult.Diagnostics</c> for
    /// provenance — Aggregation never re-deduplicates findings itself (skill.md (Phase 7B) §7),
    /// it only reports how many were already merged upstream.</summary>
    public int FindingsDeduplicatedUpstream { get; private set; }

    public int ApplicationSummariesCreated { get; private set; }
    public int ServerLevelFindingCount { get; private set; }
    public int TopRisksSelected { get; private set; }

    /// <summary>Findings whose entity could not be resolved to any ApplicationBoundary — the
    /// same count as <see cref="Models.ServerRiskSummary.ServerScopedFindingCount"/>, exposed
    /// here too as an explicit diagnostic per skill.md (Phase 7B) §16.</summary>
    public int UnresolvedOwnershipCount { get; private set; }

    public IReadOnlyList<AggregationIssue> Issues => _issues;

    public void RecordFindingsProcessed(int count) => FindingsProcessed = count;
    public void RecordFindingsDeduplicatedUpstream(int count) => FindingsDeduplicatedUpstream = count;
    public void RecordApplicationSummariesCreated(int count) => ApplicationSummariesCreated = count;
    public void RecordServerLevelFindingCount(int count) => ServerLevelFindingCount = count;
    public void RecordTopRisksSelected(int count) => TopRisksSelected = count;
    public void RecordUnresolvedOwnershipCount(int count) => UnresolvedOwnershipCount = count;

    public void RecordIssue(string stage, string message) =>
        _issues.Add(new AggregationIssue { Stage = stage, Message = message });
}
