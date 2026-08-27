using ServerSleuth.Analysis.Migration.Models;

namespace ServerSleuth.Analysis.Migration.Consolidation;

/// <summary>
/// A deterministic grouping of <see cref="MigrationDependency"/> records by their existing
/// <see cref="MigrationDependencyType"/> — see skill.md (Phase 8C) §7. Uses the exact identity
/// scheme <see cref="MigrationDependency"/> already has (<c>DependencyId</c>/<c>Type</c>); never
/// a new identity or classification scheme. A type absent from this scan simply produces no
/// group — mirroring <c>RiskSummaryBase.CategoryCounts</c>'s own "zero-count categories are
/// omitted, never listed as 0" convention from Phase 7B.
/// </summary>
public sealed record MigrationDependencyGroup
{
    public required MigrationDependencyType Type { get; init; }

    /// <summary>Ordinal-sorted by <c>DependencyId</c>; references into the same
    /// <see cref="MigrationDependency"/> instances the source assessment/plan already carry.</summary>
    public required IReadOnlyList<MigrationDependency> Dependencies { get; init; }
}
