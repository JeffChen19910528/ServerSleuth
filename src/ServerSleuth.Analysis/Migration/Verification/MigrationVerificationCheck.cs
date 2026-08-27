using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Migration.Verification;

/// <summary>
/// One declarative verification checklist item — see skill.md (Phase 8B) §9-10. Describes WHAT
/// must be confirmed before or after migration, never performs the check itself: no file/registry/
/// service/network probe of any kind (§10, §14, §23).
///
/// Always traces back to the <see cref="MigrationAction"/>(s) and/or <see cref="MigrationDependency"/>(ies)
/// that justify it (§19) — a dependency with no associated risk still receives a check (§8), but
/// never a fabricated one with no evidence behind it.
/// </summary>
public sealed record MigrationVerificationCheck
{
    /// <summary>Deterministic — see <see cref="ComputeId"/>.</summary>
    public required string CheckId { get; init; }

    public required string Title { get; init; }
    public required string Description { get; init; }
    public required MigrationVerificationPhase Phase { get; init; }

    /// <summary>Shares <see cref="MigrationActionType"/>'s taxonomy — see that type's own doc
    /// comment for why one enum describes both "the kind of action" and "the kind of check."</summary>
    public required MigrationActionType CheckType { get; init; }

    public required IReadOnlyList<string> AffectedBoundaryIds { get; init; }

    /// <summary>The MigrationAction(s) this check verifies the readiness/outcome of — ordinal
    /// sorted; empty when this check was generated directly from a Dependency with no associated
    /// action (an orphan dependency, §8, or an Informational-impact issue, §22 fixture 2).</summary>
    public required IReadOnlyList<string> RelatedActionIds { get; init; }

    public required IReadOnlyList<string> RelatedDependencyIds { get; init; }
    public required IReadOnlyList<EvidenceRecord> Evidence { get; init; }
    public required string Rationale { get; init; }

    public static string ComputeId(MigrationVerificationPhase phase, MigrationActionType checkType, string sourceId) =>
        $"check:{phase}:{checkType}:{sourceId}";
}
