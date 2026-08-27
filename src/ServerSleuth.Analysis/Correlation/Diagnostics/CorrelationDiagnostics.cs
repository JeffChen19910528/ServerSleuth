namespace ServerSleuth.Analysis.Correlation.Diagnostics;

/// <summary>
/// Auditable record of what correlation did and did not conclude, and why — see skill.md §16.
/// Every candidate a rule produces is accounted for here: it either became a new edge, was
/// merged into an existing edge's evidence, or was rejected with a reason.
/// </summary>
public sealed class CorrelationDiagnostics
{
    private readonly List<RejectedCandidate> _rejected = [];

    public int CandidatesEvaluated { get; private set; }
    public int EdgesCreated { get; private set; }
    public int DuplicatesMerged { get; private set; }
    public IReadOnlyList<RejectedCandidate> Rejected => _rejected;

    public void RecordEvaluated() => CandidatesEvaluated++;
    public void RecordEdgeCreated() => EdgesCreated++;
    public void RecordDuplicateMerged() => DuplicatesMerged++;

    public void RecordRejected(string ruleId, string sourceEntityId, string? targetHint, string reason) =>
        _rejected.Add(new RejectedCandidate { RuleId = ruleId, SourceEntityId = sourceEntityId, TargetHint = targetHint, Reason = reason });
}
