namespace ServerSleuth.Analysis.Migration.Actions;

/// <summary>
/// A <see cref="MigrationAction"/>'s priority — see skill.md (Phase 8B) §16: derived directly
/// from the originating <c>MigrationIssue</c>'s own <c>RiskSeverity</c> (Critical/High/Medium/
/// Low/Info map 1:1 to these five values), never recomputed and never increased merely because
/// an action's <see cref="MigrationAction.AffectedBoundaryIds"/> spans multiple workloads —
/// shared impact affects scope, not priority.
/// </summary>
public enum MigrationActionPriority
{
    Informational,
    Low,
    Medium,
    High,
    Critical
}
