using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Verification;

namespace ServerSleuth.Analysis.Migration.Planning;

/// <summary>
/// Phase 8B entry point — see skill.md (Phase 8B) §1:
/// <c>MigrationAssessment -&gt; MigrationActionPlanner -&gt; MigrationVerificationPlanner -&gt; MigrationPlan</c>.
///
/// Pure orchestration: never re-runs Phase 8A/7A/7B, never touches any external system, never
/// mutates the <see cref="MigrationAssessmentSummary"/> it is given (§25).
/// </summary>
public static class MigrationPlanEngine
{
    public static MigrationPlan Plan(MigrationAssessmentSummary assessment)
    {
        var (actions, actionDiagnostics) = MigrationActionPlanner.Plan(assessment.Server);
        var (preChecks, postChecks, verificationDiagnostics) = MigrationVerificationPlanner.Plan(assessment.Server, actions);

        return new MigrationPlan
        {
            Assessment = assessment,
            Actions = actions,
            Dependencies = assessment.Server.Dependencies,
            PreMigrationChecks = preChecks,
            PostMigrationChecks = postChecks,
            Diagnostics = new MigrationPlanDiagnostics { Actions = actionDiagnostics, Verification = verificationDiagnostics }
        };
    }
}
