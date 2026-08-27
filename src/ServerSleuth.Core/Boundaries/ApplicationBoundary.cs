using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Core.Boundaries;

/// <summary>
/// A logical application/workload grouping — the answer to "which discovered entities appear
/// to belong to the same application?" (skill.md §1). Deliberately NOT the same concept as
/// <see cref="ServerSleuth.Core.Models.Application"/>, which represents a single IIS site +
/// virtual path: one IIS Application may be one member of a boundary alongside a Windows
/// Service, a Scheduled Task, configuration files, and binaries that IIS knows nothing about.
///
/// This is an analysis OUTPUT (produced by ServerSleuth.Analysis's ApplicationBoundaryEngine,
/// Phase 5B), not a discovered entity — it carries no Status/Path/Architecture of its own,
/// only membership plus the evidence and confidence that justified assembling it. Membership
/// is a distinct concept from a <see cref="ServerSleuth.Core.Graph.DependencyEdge"/>: an entity
/// belonging to a boundary does not imply any entity in it depends on any other.
/// </summary>
public sealed record ApplicationBoundary
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<string> MemberEntityIds { get; init; } = [];
    public IReadOnlyList<EvidenceRecord> Evidence { get; init; } = [];
    public required Confidence Confidence { get; init; }

    /// <summary>Human-readable justification for why this boundary was assembled (and, for a
    /// merged boundary, why two anchors were merged) — e.g. "IIS Application PhysicalPath root"
    /// or "Shared execution target between service:ERPWorker and scheduledtask:\ERP\Nightly".</summary>
    public required string Reason { get; init; }
}
