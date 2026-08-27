using ServerSleuth.Analysis.Risk.Models;

namespace ServerSleuth.Analysis.Risk.Rules;

/// <summary>
/// One risk-detection rule — deterministic, side-effect-free, read-only, independently
/// testable. See skill.md (Phase 7A) §9. A rule never re-runs a scanner, never accesses the
/// filesystem/registry/process API/network, and never mutates <see cref="RiskAnalysisContext"/>.
/// </summary>
public interface IRiskRule
{
    /// <summary>Fixed, human-assigned identifier used both for deterministic rule ordering
    /// (ordinal string sort) and as part of every finding's deterministic Id — e.g.
    /// "RR1-MissingDependency". Never a GUID.</summary>
    string Id { get; }

    RiskCategory Category { get; }
    RiskSeverity DefaultSeverity { get; }

    IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context);
}
