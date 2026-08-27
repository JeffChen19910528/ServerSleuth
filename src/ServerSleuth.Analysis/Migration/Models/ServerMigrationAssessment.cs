namespace ServerSleuth.Analysis.Migration.Models;

/// <summary>
/// The whole-server migration assessment — see skill.md (Phase 8A) §7. Covers EVERY
/// RiskFinding Phase 7A/7B produced: application-scoped, server-scoped, shared-infrastructure,
/// and unresolved/global alike. Nothing is silently dropped merely because it has no
/// ApplicationBoundary attribution.
/// </summary>
public sealed record ServerMigrationAssessment : MigrationAssessmentBase
{
    public required IReadOnlyList<ApplicationMigrationAssessment> ApplicationAssessments { get; init; }
}
