namespace ServerSleuth.Analysis.Correlation.Validation;

/// <summary>A cycle is an observation, never automatically an error — see skill.md (Phase 5D)
/// §12-13. Classification only reflects whether the relationship types involved would
/// semantically forbid a cycle (Strong) or not (Weak); the validator never deletes or rejects
/// either kind.</summary>
public enum CycleClassification
{
    /// <summary>Involves at least one relationship type (Runs/Imports/DependsOn) whose
    /// semantics assume a one-directional dependency — a cycle here is surprising and worth
    /// a closer look, but is still only reported, never removed.</summary>
    Strong,

    /// <summary>Involves only relationship types (Contains/References/etc.) where a cycle is
    /// unremarkable (e.g. two configuration files each referencing the other's external
    /// dependency set indirectly).</summary>
    Weak
}
