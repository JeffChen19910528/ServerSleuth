namespace ServerSleuth.Analysis.Risk.Diagnostics;

public sealed record RuleFailure
{
    public required string RuleId { get; init; }
    public required string Message { get; init; }
}

public sealed record EvidenceInvariantViolation
{
    public required string RuleId { get; init; }
    public required string AttemptedFindingId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>
/// Auditable, deterministic record of every Risk Analysis run — see skill.md (Phase 7A) §26.
/// A rule that fails or a finding that violates the evidence invariant never disappears
/// silently: it becomes one of these entries.
/// </summary>
public sealed class RiskDiagnostics
{
    private readonly List<RuleFailure> _ruleFailures = [];
    private readonly List<EvidenceInvariantViolation> _evidenceInvariantViolations = [];

    public int RulesEvaluated { get; private set; }
    public int FindingsCreated { get; private set; }
    public int FindingsDeduplicated { get; private set; }

    public IReadOnlyList<RuleFailure> RuleFailures => _ruleFailures;
    public IReadOnlyList<EvidenceInvariantViolation> EvidenceInvariantViolations => _evidenceInvariantViolations;

    public void RecordRuleEvaluated() => RulesEvaluated++;
    public void RecordFindingCreated() => FindingsCreated++;
    public void RecordFindingsDeduplicated(int mergedAwayCount) => FindingsDeduplicated += mergedAwayCount;

    public void RecordRuleFailure(string ruleId, string message) =>
        _ruleFailures.Add(new RuleFailure { RuleId = ruleId, Message = message });

    public void RecordEvidenceInvariantViolation(string ruleId, string attemptedFindingId, string reason) =>
        _evidenceInvariantViolations.Add(new EvidenceInvariantViolation { RuleId = ruleId, AttemptedFindingId = attemptedFindingId, Reason = reason });
}
