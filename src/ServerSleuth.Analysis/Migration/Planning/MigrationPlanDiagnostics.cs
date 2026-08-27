using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Verification;

namespace ServerSleuth.Analysis.Migration.Planning;

/// <summary>Rolls up <see cref="MigrationActionPlanner"/>'s and
/// <see cref="MigrationVerificationPlanner"/>'s own diagnostics for one <see cref="MigrationPlanEngine"/>
/// run — mirrors <c>MigrationAssessmentSummary.Diagnostics</c>'s role in Phase 8A.</summary>
public sealed record MigrationPlanDiagnostics
{
    public required MigrationActionDiagnostics Actions { get; init; }
    public required MigrationVerificationDiagnostics Verification { get; init; }
}
