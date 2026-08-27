using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Boundaries.Diagnostics;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Risk;

/// <summary>
/// Immutable, read-only view over everything Discovery → Correlation → Boundary →
/// Expansion → Validation already produced — see skill.md (Phase 7A) §8. Built once per Risk
/// Analysis run; every index is computed up front so no rule ever re-scans the full entity
/// list. Risk Analysis never re-runs a scanner and never touches the filesystem/registry/
/// process API/network/systemd/Docker/Kubernetes — this type only ever reads from the
/// already-produced, already-in-memory artifacts passed to its constructor.
/// </summary>
public sealed class RiskAnalysisContext
{
    public IReadOnlyList<DiscoveryEntity> AllEntities { get; }
    public IReadOnlyDictionary<string, DiscoveryEntity> ById { get; }

    /// <summary>The expanded graph (Phase 5C output) — includes ExternalDependency nodes and
    /// Configuration→REFERENCES edges on top of the Phase 5A graph.</summary>
    public DependencyGraph Graph { get; }

    public IReadOnlyList<ApplicationBoundary> Boundaries { get; }
    public BoundaryDiagnostics BoundaryDiagnostics { get; }
    public DependencyExpansionResult Expansion { get; }
    public GraphValidationResult Validation { get; }

    public IReadOnlyList<Service> Services { get; }
    public IReadOnlyList<ScheduledTask> ScheduledTasks { get; }
    public IReadOnlyList<ComComponent> ComComponents { get; }
    public IReadOnlyList<Certificate> Certificates { get; }
    public IReadOnlyList<Configuration> Configurations { get; }
    public IReadOnlyList<Dll> Dlls { get; }
    public IReadOnlyList<Runtime> Runtimes { get; }
    public IReadOnlyList<Sdk> Sdks { get; }
    public IReadOnlyList<ExternalDependency> ExternalDependencies { get; }

    /// <summary>Entity Id → the Id of ONE ApplicationBoundary it's a member of — a deterministic
    /// (ordinal-smallest) pick, kept only for rules that assume single ownership of an
    /// otherwise-uniquely-owned entity (Certificate/COM/Configuration/etc.). Most entities are
    /// members of exactly one boundary, in which case this and <see cref="BoundaryIdsByEntityId"/>
    /// agree. A shared execution target (skill.md (Phase 5B) §8: three-or-more workloads RUNS-ing
    /// the same binary) is legitimately a member of every one of those boundaries at once — for
    /// that case, prefer <see cref="BoundaryIdsByEntityId"/>, which preserves all of them; this
    /// property intentionally picks only the ordinal-smallest so it stays a deterministic single
    /// value rather than silently depending on <c>Boundaries</c> enumeration order.</summary>
    public IReadOnlyDictionary<string, string> BoundaryIdByEntityId { get; }

    /// <summary>Entity Id → EVERY ApplicationBoundary Id it's a member of, ordinal-sorted and
    /// deduplicated — see the corrective "Shared Infrastructure Attribution Hardening" task.
    /// Never depends on <c>Boundaries</c> enumeration order: built by grouping every
    /// (entityId, boundaryId) membership pair first, then sorting each entity's boundary list.
    /// This is the authoritative source for "which boundaries does this entity affect" —
    /// <see cref="BoundaryIdByEntityId"/> is a deliberately-narrowed single-value convenience
    /// derived from it, not the other way around.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> BoundaryIdsByEntityId { get; }

    public RiskAnalysisContext(
        IReadOnlyList<DiscoveryEntity> allEntities,
        DependencyGraph expandedGraph,
        BoundaryAnalysisResult boundaryResult,
        DependencyExpansionResult expansion,
        GraphValidationResult validation)
    {
        AllEntities = allEntities;
        Graph = expandedGraph;
        Boundaries = boundaryResult.Boundaries;
        BoundaryDiagnostics = boundaryResult.Diagnostics;
        Expansion = expansion;
        Validation = validation;

        var byId = new Dictionary<string, DiscoveryEntity>();
        foreach (var entity in allEntities)
        {
            byId.TryAdd(entity.Id, entity);
        }
        // ExternalDependency nodes only exist in the expanded graph, not the original
        // discovery entity list — index them too so ById covers every node a rule might need
        // to resolve by RelatedEntityId.
        foreach (var node in Graph.Nodes)
        {
            byId.TryAdd(node.Id, node);
        }
        ById = byId;

        Services = allEntities.OfType<Service>().ToList();
        ScheduledTasks = allEntities.OfType<ScheduledTask>().ToList();
        ComComponents = allEntities.OfType<ComComponent>().ToList();
        Certificates = allEntities.OfType<Certificate>().ToList();
        Configurations = allEntities.OfType<Configuration>().ToList();
        Dlls = allEntities.OfType<Dll>().ToList();
        Runtimes = allEntities.OfType<Runtime>().ToList();
        Sdks = allEntities.OfType<Sdk>().ToList();
        ExternalDependencies = expansion.ExternalDependencies;

        // Group every (entityId -> boundaryId) membership pair first — regardless of how many
        // boundaries claim a given entity — then sort deterministically. This is what makes
        // both maps below immune to `Boundaries`' own enumeration order.
        var boundariesByEntity = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var boundary in Boundaries)
        {
            foreach (var memberId in boundary.MemberEntityIds)
            {
                if (!boundariesByEntity.TryGetValue(memberId, out var list))
                {
                    boundariesByEntity[memberId] = list = [];
                }

                if (!list.Contains(boundary.Id, StringComparer.Ordinal))
                {
                    list.Add(boundary.Id);
                }
            }
        }

        var boundaryIdsByEntity = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var boundaryIdByEntity = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (entityId, boundaryIds) in boundariesByEntity)
        {
            var sorted = boundaryIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
            boundaryIdsByEntity[entityId] = sorted;
            boundaryIdByEntity[entityId] = sorted[0];
        }

        BoundaryIdsByEntityId = boundaryIdsByEntity;
        BoundaryIdByEntityId = boundaryIdByEntity;
    }
}
