namespace ServerSleuth.Analysis.Risk.Models;

/// <summary>
/// The aggregate classification a <c>RiskSummary</c> carries — see skill.md (Phase 7B) §2, §6.
/// Deliberately distinct from <see cref="RiskSeverity"/> (which only ever describes one
/// RiskFinding): an aggregate can be <see cref="None"/> when zero findings exist, a state no
/// single finding can ever be in. Declared in ascending severity order on purpose so ordinal
/// enum comparison (<c>a &gt; b</c>) is the deterministic ordering — never string-sort this.
/// </summary>
public enum AggregateSeverity
{
    None,
    Info,
    Low,
    Medium,
    High,
    Critical
}

public static class AggregateSeverityExtensions
{
    /// <summary>Widening conversion — every <see cref="RiskSeverity"/> value has an identically-
    /// named <see cref="AggregateSeverity"/> counterpart; only <see cref="AggregateSeverity.None"/>
    /// has no RiskSeverity equivalent, since no single finding can ever be "no risk."</summary>
    public static AggregateSeverity ToAggregateSeverity(this RiskSeverity severity) => severity switch
    {
        RiskSeverity.Info => AggregateSeverity.Info,
        RiskSeverity.Low => AggregateSeverity.Low,
        RiskSeverity.Medium => AggregateSeverity.Medium,
        RiskSeverity.High => AggregateSeverity.High,
        RiskSeverity.Critical => AggregateSeverity.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
    };
}
