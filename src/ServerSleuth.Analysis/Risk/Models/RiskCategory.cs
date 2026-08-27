namespace ServerSleuth.Analysis.Risk.Models;

/// <summary>
/// Controlled risk taxonomy — see skill.md (Phase 7A) §4. Every RiskFinding belongs to exactly
/// one category; this list is deliberately small and closed rather than growing per-rule.
/// </summary>
public enum RiskCategory
{
    MissingDependency,
    AccessDenied,
    MissingBinary,
    MissingRuntime,
    Certificate,
    Service,
    ScheduledTask,
    Com,
    ExternalDependency,
    SharedInfrastructure,
    Configuration,
    GraphIntegrity
}
