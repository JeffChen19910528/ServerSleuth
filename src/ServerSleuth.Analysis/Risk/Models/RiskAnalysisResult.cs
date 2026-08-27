using ServerSleuth.Analysis.Risk.Diagnostics;

namespace ServerSleuth.Analysis.Risk.Models;

/// <summary>The deterministic output of one <see cref="Engine.RiskRuleEngine"/> run — see
/// skill.md (Phase 7A) §10.</summary>
public sealed record RiskAnalysisResult
{
    /// <summary>Sorted deterministically — see <see cref="Engine.RiskRuleEngine"/> for the exact
    /// ordering rule. Never left in rule-registration or dictionary-enumeration order.</summary>
    public required IReadOnlyList<RiskFinding> Findings { get; init; }

    public required RiskDiagnostics Diagnostics { get; init; }
}
