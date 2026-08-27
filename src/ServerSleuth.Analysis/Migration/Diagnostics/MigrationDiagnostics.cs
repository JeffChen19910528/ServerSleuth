namespace ServerSleuth.Analysis.Migration.Diagnostics;

public sealed record UnclassifiedImpact
{
    public required string RuleId { get; init; }
    public required string FindingId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>
/// Auditable, deterministic record of one Migration Assessment run — see skill.md (Phase 8A)
/// §16. Mirrors <c>RiskDiagnostics</c>/<c>RiskAggregationDiagnostics</c>'s philosophy: nothing
/// about policy classification ever happens silently, including when the policy itself has no
/// answer.
/// </summary>
public sealed class MigrationDiagnostics
{
    private readonly List<UnclassifiedImpact> _unclassifiedImpacts = [];

    public int FindingsEvaluated { get; private set; }
    public int IssuesCreated { get; private set; }
    public int DependenciesCreated { get; private set; }
    public int ApplicationAssessmentsCreated { get; private set; }

    public IReadOnlyList<UnclassifiedImpact> UnclassifiedImpacts => _unclassifiedImpacts;

    public void RecordFindingEvaluated() => FindingsEvaluated++;
    public void RecordIssueCreated() => IssuesCreated++;
    public void RecordDependencyCreated() => DependenciesCreated++;
    public void RecordApplicationAssessmentsCreated(int count) => ApplicationAssessmentsCreated = count;

    public void RecordUnclassifiedImpact(string ruleId, string findingId, string reason) =>
        _unclassifiedImpacts.Add(new UnclassifiedImpact { RuleId = ruleId, FindingId = findingId, Reason = reason });
}
