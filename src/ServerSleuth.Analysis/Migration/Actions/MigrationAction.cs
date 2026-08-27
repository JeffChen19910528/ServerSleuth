using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Migration.Actions;

/// <summary>
/// A platform-neutral, declarative migration action — see skill.md (Phase 8B) §4. A
/// recommendation/plan entry only: describes WHAT must be addressed before or after migration,
/// never HOW the tool would execute it (§5, §23) — no shell command, no copy operation, no
/// service/registry/IIS mutation is ever generated or performed.
///
/// Always traceable back to the exact <see cref="MigrationIssue"/>(s) (via
/// <see cref="RelatedIssueIds"/>) and/or <see cref="MigrationDependency"/>(ies) (via
/// <see cref="RelatedDependencyIds"/>) that justify it — never fabricated (§19). Because Phase 7A
/// already merges cross-rule findings about the same missing binary (the
/// <c>MissingBinaryEntityId</c> anchor — see <c>RiskRuleEngine.Deduplicate</c>) and Phase 8A
/// already fans a shared dependency's <c>AffectedBoundaryIds</c> out onto one Issue rather than
/// duplicating the Issue per boundary, one <see cref="MigrationAction"/> per originating Issue is
/// already the correct "one logical action, several affected workloads" shape (§7, §17, §20) —
/// <see cref="MigrationActionPlanner"/> additionally folds in any <see cref="MigrationDependency"/>
/// that traces back to the same Issue via <c>RelatedRiskFindingId</c>, so a requirement backed by
/// both never produces two actions.
/// </summary>
public sealed record MigrationAction
{
    /// <summary>Deterministic: <c>action:{ActionType}:{IssueId}</c> — see <see cref="ComputeId"/>.</summary>
    public required string ActionId { get; init; }

    public required MigrationActionType ActionType { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required MigrationActionPriority Priority { get; init; }

    /// <summary>Always <see cref="MigrationVerificationPhase.PreMigration"/> — every action kind
    /// this planner produces is a preparation, review, or inventory-confirmation task meant to be
    /// addressed before cutover (§2). Present as a field (rather than hard-coded) because §4
    /// explicitly asks for it and a future action kind could legitimately be post-migration.</summary>
    public required MigrationVerificationPhase Phase { get; init; }

    /// <summary>Every ApplicationBoundary this action affects — ordinal-sorted; carried through
    /// unchanged from the originating Issue, so a shared dependency's action still lists every
    /// affected boundary (§7, §20) without duplicating the action itself.</summary>
    public required IReadOnlyList<string> AffectedBoundaryIds { get; init; }

    public required IReadOnlyList<string> AffectedEntityIds { get; init; }

    /// <summary>The MigrationIssue(s) this action traces back to — ordinal-sorted. Exactly one
    /// entry today (Phase 7A/8A already collapse duplicates before this planner ever runs), kept
    /// as a list because the identity rule (§17) is about logical requirements, not cardinality.</summary>
    public required IReadOnlyList<string> RelatedIssueIds { get; init; }

    /// <summary>Every MigrationDependency whose <c>RelatedRiskFindingId</c> traces back to the
    /// same originating Issue — ordinal-sorted; empty when no dependency exists for this issue.</summary>
    public required IReadOnlyList<string> RelatedDependencyIds { get; init; }

    /// <summary>The originating Issue's own evidence, unchanged — never fabricated.</summary>
    public required IReadOnlyList<EvidenceRecord> Evidence { get; init; }

    /// <summary>Why this action exists — carried from the originating Issue's own
    /// <c>PolicyDecisionReason</c>, never a fresh explanation invented at this layer.</summary>
    public required string Rationale { get; init; }

    public static string ComputeId(MigrationActionType actionType, string issueId) => $"action:{actionType}:{issueId}";
}
