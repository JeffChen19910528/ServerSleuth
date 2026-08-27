using ServerSleuth.Core.Models;

namespace ServerSleuth.Core.Graph;

/// <summary>
/// The dependency graph: entities as nodes, DependencyEdge as edges. Purely structural —
/// no analysis or risk logic lives here (that belongs in ServerSleuth.Analysis). See skill.md §21.
///
/// <see cref="EdgesFrom"/>/<see cref="EdgesTo"/>/<see cref="AddEdge"/>'s duplicate-merge check
/// are indexed — see skill.md (Phase 10A-I) §3-6. <see cref="_edges"/> remains the single
/// authoritative, ordered edge collection; <see cref="_edgeIndicesBySource"/>/
/// <see cref="_edgeIndicesByTarget"/> are pure lookup accelerators over it, never exposed
/// publicly, and never a second source of truth for edge content — each stores the INDEX of an
/// edge within <see cref="_edges"/> rather than a second reference to the edge itself, so
/// <see cref="AddEdge"/>'s existing evidence-merge/highest-confidence-wins behavior can update
/// the one authoritative copy in place (a single O(1) <c>_edges[index] = merged</c> write) without
/// needing to also relocate it inside two more collections. This is a purely internal
/// implementation detail — conceptually equivalent to the <c>Dictionary&lt;string,
/// List&lt;DependencyEdge&gt;&gt;</c> shape a lookup index would normally take, adapted so an
/// in-place merge stays correct without a second linear scan.
///
/// Not thread-safe — no concurrent-collection type was introduced, matching this type's existing
/// (undocumented, but consistently single-threaded-caller) usage throughout the codebase; nothing
/// prior to this phase synchronized access to it either.
/// </summary>
public sealed class DependencyGraph
{
    private readonly Dictionary<string, DiscoveryEntity> _nodes = new();
    private readonly List<DependencyEdge> _edges = [];

    /// <summary>Entity Id → indices into <see cref="_edges"/> of every edge whose
    /// <c>SourceEntityId</c> equals that Id, in the same relative order those edges were
    /// inserted overall — i.e. <see cref="EdgesFrom"/> returns exactly what
    /// <c>_edges.Where(e =&gt; e.SourceEntityId == id)</c> already returned before this phase,
    /// just without the O(edge-count) scan.</summary>
    private readonly Dictionary<string, List<int>> _edgeIndicesBySource = new(StringComparer.Ordinal);

    /// <summary>Same as <see cref="_edgeIndicesBySource"/>, keyed by <c>TargetEntityId</c>.</summary>
    private readonly Dictionary<string, List<int>> _edgeIndicesByTarget = new(StringComparer.Ordinal);

    public IReadOnlyCollection<DiscoveryEntity> Nodes => _nodes.Values;
    public IReadOnlyList<DependencyEdge> Edges => _edges;

    /// <summary>Adds a node. Throws if an entity with the same Id is already present —
    /// entity deduplication/merging must happen upstream, in Correlation, before graph
    /// assembly (see skill.md §32).</summary>
    public void AddNode(DiscoveryEntity entity)
    {
        if (!_nodes.TryAdd(entity.Id, entity))
        {
            throw new InvalidOperationException(
                $"A node with Id '{entity.Id}' already exists in the graph. Merge duplicate entities before adding them to the graph.");
        }
    }

    public bool TryGetNode(string id, out DiscoveryEntity? entity) => _nodes.TryGetValue(id, out entity);

    /// <summary>
    /// Adds an edge. If an edge with the same source/target/type already exists, the two are
    /// merged: evidence is unioned and the higher confidence wins, rather than producing a
    /// duplicate edge for the same logical relationship — identical semantics to before this
    /// phase (skill.md §2, §4), only the lookup used to find the existing edge is now indexed
    /// (O(out-degree of <c>edge.SourceEntityId</c>) instead of O(total edge count)).
    /// </summary>
    public void AddEdge(DependencyEdge edge)
    {
        var existingIndex = FindExistingEdgeIndex(edge);

        if (existingIndex < 0)
        {
            var newIndex = _edges.Count;
            _edges.Add(edge);
            AddIndexEntry(_edgeIndicesBySource, edge.SourceEntityId, newIndex);
            AddIndexEntry(_edgeIndicesByTarget, edge.TargetEntityId, newIndex);
            return;
        }

        var existing = _edges[existingIndex];
        var mergedEvidence = existing.Evidence.Concat(edge.Evidence).ToList();
        var mergedConfidence = edge.Confidence.Value > existing.Confidence.Value ? edge.Confidence : existing.Confidence;

        // Source/Target/Type are unchanged by a merge, so the index entries recorded for the
        // original insertion already point at the right slot in _edges — only the slot's own
        // content (Evidence/Confidence) needs updating, never the indices themselves.
        _edges[existingIndex] = existing with
        {
            Evidence = mergedEvidence,
            Confidence = mergedConfidence
        };
    }

    public IEnumerable<DependencyEdge> EdgesFrom(string sourceEntityId) =>
        _edgeIndicesBySource.TryGetValue(sourceEntityId, out var indices)
            ? indices.Select(i => _edges[i])
            : [];

    public IEnumerable<DependencyEdge> EdgesTo(string targetEntityId) =>
        _edgeIndicesByTarget.TryGetValue(targetEntityId, out var indices)
            ? indices.Select(i => _edges[i])
            : [];

    private int FindExistingEdgeIndex(DependencyEdge edge)
    {
        if (!_edgeIndicesBySource.TryGetValue(edge.SourceEntityId, out var indices))
        {
            return -1;
        }

        foreach (var index in indices)
        {
            var candidate = _edges[index];
            if (candidate.TargetEntityId == edge.TargetEntityId && candidate.Type == edge.Type)
            {
                return index;
            }
        }

        return -1;
    }

    private static void AddIndexEntry(Dictionary<string, List<int>> index, string key, int edgeIndex)
    {
        if (!index.TryGetValue(key, out var list))
        {
            index[key] = list = [];
        }

        list.Add(edgeIndex);
    }
}
