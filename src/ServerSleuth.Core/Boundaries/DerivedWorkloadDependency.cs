using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Core.Boundaries;

/// <summary>
/// A workload-level relationship derived from a chain of existing entity-level evidence — e.g.
/// "ApplicationBoundary ERP references ExternalDependency DB01" derived from
/// Configuration→REFERENCES→ExternalDependency plus that Configuration's membership in the ERP
/// boundary. This is deliberately NOT a <see cref="ServerSleuth.Core.Graph.DependencyEdge"/>:
/// an <see cref="ApplicationBoundary"/> is not a <see cref="ServerSleuth.Core.Models.DiscoveryEntity"/>
/// and does not belong in <see cref="ServerSleuth.Core.Graph.DependencyGraph"/>'s node set, and
/// collapsing the two would blur "this entity depends on that entity" (proven directly) with
/// "this workload appears to depend on that thing" (proven only by tracing through membership).
/// <see cref="DerivedFrom"/> preserves that provenance chain explicitly so an auditor can
/// retrace it — see skill.md (Phase 5C) §21-22.
/// </summary>
public sealed record DerivedWorkloadDependency
{
    public required string BoundaryId { get; init; }
    public required string TargetEntityId { get; init; }
    public required DependencyEdgeType Type { get; init; }
    public required Confidence Confidence { get; init; }
    public IReadOnlyList<EvidenceRecord> Evidence { get; init; } = [];

    /// <summary>Human-readable evidence chain, e.g. "ApplicationBoundary owns Configuration
    /// D:\ERP\Web\web.config, which REFERENCES ExternalDependency database:sqlserver:db01".</summary>
    public required string DerivedFrom { get; init; }
}
