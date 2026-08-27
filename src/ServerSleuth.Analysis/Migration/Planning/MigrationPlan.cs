using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Verification;

namespace ServerSleuth.Analysis.Migration.Planning;

/// <summary>
/// The deterministic, evidence-backed migration plan produced by <see cref="MigrationPlanEngine"/>
/// — see skill.md (Phase 8B) §1, §3. Planning-only: describes what must be done before migration,
/// what must be prepared, and what must be verified after — never executes anything (§2, §23).
///
/// <see cref="Dependencies"/> is the exact same list already carried on
/// <see cref="Migration.Models.MigrationAssessmentBase.Dependencies"/> (via <see cref="Assessment"/>),
/// surfaced at the top level for convenience per §3 — never a second, separately-derived copy of
/// dependency data (§3: "Do not duplicate existing MigrationAssessment models").
/// </summary>
public sealed record MigrationPlan
{
    public required MigrationAssessmentSummary Assessment { get; init; }
    public required IReadOnlyList<MigrationAction> Actions { get; init; }
    public required IReadOnlyList<MigrationDependency> Dependencies { get; init; }
    public required IReadOnlyList<MigrationVerificationCheck> PreMigrationChecks { get; init; }
    public required IReadOnlyList<MigrationVerificationCheck> PostMigrationChecks { get; init; }
    public required MigrationPlanDiagnostics Diagnostics { get; init; }
}
