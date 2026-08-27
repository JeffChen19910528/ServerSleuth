namespace ServerSleuth.Analysis.Risk.Models;

/// <summary>See skill.md (Phase 7A) §5. Severity comes from the rule that produced the finding
/// — never computed by arbitrary arithmetic. No 0-100 score exists in Phase 7A; that is
/// explicitly deferred to a later Risk Scoring phase.</summary>
public enum RiskSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}
