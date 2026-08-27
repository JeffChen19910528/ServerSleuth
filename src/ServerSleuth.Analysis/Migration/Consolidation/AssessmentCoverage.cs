namespace ServerSleuth.Analysis.Migration.Consolidation;

/// <summary>
/// How complete the underlying discovery evidence was for this migration assessment — see
/// skill.md (Phase 8C) §11-13. Derived ONLY from already-produced scanner outcome information
/// (<see cref="ServerSleuth.Core.Orchestration.AggregateDiscoveryResult"/>) — never invented, and
/// deliberately NOT a factor in <see cref="Models.MigrationStatus"/> computation (§12): a server
/// with `Coverage = Partial` and no findings is still `MigrationStatus.Ready` — the existing
/// `MigrationPolicy` from Phase 8A remains the sole authority for whether missing evidence itself
/// is a migration blocker (e.g. RR3-AccessDenied already produces a RemediationRequired issue on
/// its own; Coverage never independently downgrades status on top of that).
///
/// Policy (see <see cref="ServerMigrationAssessmentReportEngine"/> for the implementation):
/// <list type="bullet">
/// <item><see cref="Unknown"/> — no discovery result was supplied at all, or it contained zero
/// scanner results. There is no evidence to judge completeness from.</item>
/// <item><see cref="Limited"/> — at least one scanner reported <c>AccessDenied</c> or
/// <c>Failed</c>: evidence for that area of the system could not be gathered at all.</item>
/// <item><see cref="Partial"/> — no AccessDenied/Failed scanner, but at least one
/// <c>PartiallySupported</c>: evidence was gathered, but the scanner itself flagged its own
/// output as incomplete.</item>
/// <item><see cref="Complete"/> — every scanner reported <c>Supported</c>,
/// <c>NotApplicable</c>, or <c>NotInstalled</c>. NotApplicable/NotInstalled are treated as
/// neutral, not degraded: "there was nothing here to discover" is not an evidence gap.</item>
/// </list>
/// </summary>
public enum AssessmentCoverage
{
    Unknown,
    Limited,
    Partial,
    Complete
}
