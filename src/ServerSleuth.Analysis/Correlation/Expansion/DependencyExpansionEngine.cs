using System.Text.RegularExpressions;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Correlation.Expansion;

/// <summary>
/// Connects currently-isolated discovery information (Certificates, COM registrations,
/// Configuration's Database/Endpoint/UNC/Runtime observations) to the logical workloads Phase
/// 5B established — skill.md (Phase 5C) §1. Consumes Phase 5A's <see cref="DependencyGraph"/>
/// and Phase 5B's <see cref="ApplicationBoundary"/> list as evidence; performs no new
/// discovery, filesystem/registry/process/network access (skill.md §27, §33).
/// </summary>
public sealed class DependencyExpansionEngine
{
    private static readonly Regex TargetFrameworkMajorVersion = new(@"^net(?<major>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public DependencyExpansionResult Expand(
        IReadOnlyList<DiscoveryEntity> entities,
        DependencyGraph graph,
        IReadOnlyList<ApplicationBoundary> boundaries)
    {
        var context = new CorrelationContext(entities);
        var diagnostics = new ExpansionDiagnostics();
        var expandedGraph = CloneGraph(graph);

        var dependencies = BuildExternalDependencies(context.Configurations, expandedGraph, diagnostics);
        AddRuntimeVersionEdges(context, expandedGraph, diagnostics);

        var derived = new List<DerivedWorkloadDependency>();
        derived.AddRange(BuildCertificateAssociations(boundaries, context, graph, diagnostics));
        derived.AddRange(BuildComAssociations(boundaries, context, graph, diagnostics));
        derived.AddRange(BuildExternalDependencyWorkloadAssociations(boundaries, expandedGraph, diagnostics));

        return new DependencyExpansionResult
        {
            ExternalDependencies = dependencies,
            ExpandedGraph = expandedGraph,
            DerivedWorkloadDependencies = derived,
            Diagnostics = diagnostics
        };
    }

    private static DependencyGraph CloneGraph(DependencyGraph source)
    {
        var clone = new DependencyGraph();
        foreach (var node in source.Nodes)
        {
            clone.AddNode(node);
        }

        foreach (var edge in source.Edges)
        {
            clone.AddEdge(edge);
        }

        return clone;
    }

    /// <summary>Materializes ExternalDependency entities from Configuration's already-detected
    /// metadata (skill.md §17) and adds Configuration→REFERENCES→ExternalDependency edges.
    /// Identical dependencies observed across multiple configuration files merge into one
    /// entity with unioned evidence (skill.md §25).</summary>
    private static List<ExternalDependency> BuildExternalDependencies(
        IReadOnlyList<Configuration> configurations,
        DependencyGraph expandedGraph,
        ExpansionDiagnostics diagnostics)
    {
        var byId = new Dictionary<string, ExternalDependency>(StringComparer.Ordinal);

        foreach (var configuration in configurations.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            foreach (var extracted in ExternalDependencyExtractor.Extract(configuration))
            {
                if (byId.TryGetValue(extracted.Entity.Id, out var existing))
                {
                    existing.AddEvidence(new EvidenceRecord
                    {
                        Type = EvidenceType.ConfigurationFile,
                        Location = configuration.Path ?? configuration.Id,
                        Detail = extracted.ReferenceDetail
                    });
                    diagnostics.RecordExternalDependencyMerged();
                }
                else
                {
                    extracted.Entity.AddEvidence(new EvidenceRecord
                    {
                        Type = EvidenceType.ConfigurationFile,
                        Location = configuration.Path ?? configuration.Id,
                        Detail = extracted.ReferenceDetail
                    });
                    byId[extracted.Entity.Id] = extracted.Entity;
                    diagnostics.RecordExternalDependencyCreated();
                }

                expandedGraph.AddEdge(new DependencyEdge
                {
                    SourceEntityId = configuration.Id,
                    TargetEntityId = extracted.Entity.Id,
                    Type = DependencyEdgeType.References,
                    Confidence = Confidence.Medium(),
                    Evidence =
                    [
                        new EvidenceRecord
                        {
                            Type = EvidenceType.ConfigurationFile,
                            Location = configuration.Path ?? configuration.Id,
                            Detail = extracted.ReferenceDetail
                        }
                    ]
                });
            }
        }

        foreach (var dependency in byId.Values)
        {
            if (!expandedGraph.TryGetNode(dependency.Id, out _))
            {
                expandedGraph.AddNode(dependency);
            }
        }

        return byId.Values.ToList();
    }

    /// <summary>Only ever links a Configuration to a Runtime when an EXPLICIT version was
    /// detected (skill.md §18-19) — never selects among multiple installed versions when only
    /// a bare family marker exists (that remains Phase 5A's existing Low-confidence, all-
    /// matches behavior, untouched by this phase).</summary>
    private static void AddRuntimeVersionEdges(CorrelationContext context, DependencyGraph expandedGraph, ExpansionDiagnostics diagnostics)
    {
        const string prefix = "RuntimeVersion: ";

        foreach (var configuration in context.Configurations)
        {
            foreach (var reference in configuration.DetectedDependencyReferences)
            {
                if (!reference.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var tfm = reference[prefix.Length..];
                var majorMatch = TargetFrameworkMajorVersion.Match(tfm);
                if (!majorMatch.Success)
                {
                    diagnostics.RecordUnresolvedRuntimeReference(configuration.Id, $"Target framework moniker '{tfm}' did not match the expected netX[.Y] shape");
                    continue;
                }

                var major = majorMatch.Groups["major"].Value;
                var matches = context.Runtimes
                    .Where(r => IsDotNetFamily(r) && r.Version is not null && r.Version.StartsWith(major + ".", StringComparison.Ordinal))
                    .ToList();

                if (matches.Count == 0)
                {
                    diagnostics.RecordUnresolvedRuntimeReference(configuration.Id, $"No installed .NET runtime/SDK matches target framework '{tfm}'");
                    continue;
                }

                foreach (var runtime in matches)
                {
                    expandedGraph.AddEdge(new DependencyEdge
                    {
                        SourceEntityId = configuration.Id,
                        TargetEntityId = runtime.Id,
                        Type = DependencyEdgeType.References,
                        Confidence = Confidence.High(),
                        Evidence =
                        [
                            new EvidenceRecord
                            {
                                Type = EvidenceType.ConfigurationFile,
                                Location = configuration.Path ?? configuration.Id,
                                Detail = $"TargetFramework={tfm} matched installed runtime version {runtime.Version}"
                            }
                        ]
                    });
                }
            }
        }
    }

    private static bool IsDotNetFamily(Runtime runtime) =>
        (runtime.Metadata.GetValueOrDefault("Family") ?? runtime.Type).StartsWith("DotNet", StringComparison.Ordinal);

    /// <summary>ApplicationBoundary → Certificate, derived from Application→(HOSTED BY)→Site→
    /// BINDS_TO→Certificate — see skill.md §3, §24. Thumbprint (via the existing BINDS_TO edge)
    /// is the only identifier used; never a name/subject/CN match.</summary>
    private static List<DerivedWorkloadDependency> BuildCertificateAssociations(
        IReadOnlyList<ApplicationBoundary> boundaries,
        CorrelationContext context,
        DependencyGraph graph,
        ExpansionDiagnostics diagnostics)
    {
        var result = new List<DerivedWorkloadDependency>();

        foreach (var boundary in boundaries)
        {
            foreach (var memberId in boundary.MemberEntityIds)
            {
                if (!context.ById.TryGetValue(memberId, out var member) || member is not Application)
                {
                    continue;
                }

                var siteIds = graph.EdgesTo(memberId).Where(e => e.Type == DependencyEdgeType.Hosts).Select(e => e.SourceEntityId);

                foreach (var siteId in siteIds)
                {
                    foreach (var bindEdge in graph.EdgesFrom(siteId).Where(e => e.Type == DependencyEdgeType.Binds))
                    {
                        result.Add(new DerivedWorkloadDependency
                        {
                            BoundaryId = boundary.Id,
                            TargetEntityId = bindEdge.TargetEntityId,
                            Type = DependencyEdgeType.Binds,
                            Confidence = new Confidence(Math.Min(boundary.Confidence.Value, bindEdge.Confidence.Value)),
                            Evidence = bindEdge.Evidence,
                            DerivedFrom = $"ApplicationBoundary {boundary.Id} owns Application {memberId}, hosted by Site {siteId}, which BINDS_TO Certificate {bindEdge.TargetEntityId}"
                        });
                        diagnostics.RecordCertificateAssociation();
                    }
                }
            }
        }

        return result;
    }

    /// <summary>ApplicationBoundary → COM Component, only when explicit evidence connects the
    /// COM registration (or the binary it references) to a boundary member — never a blanket
    /// "every COM on the server belongs to every workload" association. See skill.md §5-6, §23.</summary>
    private static List<DerivedWorkloadDependency> BuildComAssociations(
        IReadOnlyList<ApplicationBoundary> boundaries,
        CorrelationContext context,
        DependencyGraph graph,
        ExpansionDiagnostics diagnostics)
    {
        var result = new List<DerivedWorkloadDependency>();
        var allBoundaryMembers = new HashSet<string>(boundaries.SelectMany(b => b.MemberEntityIds), StringComparer.Ordinal);

        // Built once for the whole expansion stage — never per boundary (skill.md Phase 10A-I
        // §10). Binary entity id -> every COM component with a References edge targeting that
        // binary, preserving context.ComComponents' own relative order among COMs that share a
        // target (graph.EdgesFrom is now indexed, so this pass itself is O(ComComponents) rather
        // than the old O(Boundaries * ComComponents * Edges) triple scan).
        var comsByReferencedBinary = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var comOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < context.ComComponents.Count; i++)
        {
            var com = context.ComComponents[i];
            comOrder[com.Id] = i;

            foreach (var target in graph.EdgesFrom(com.Id).Where(e => e.Type == DependencyEdgeType.References).Select(e => e.TargetEntityId))
            {
                if (!comsByReferencedBinary.TryGetValue(target, out var list))
                {
                    comsByReferencedBinary[target] = list = [];
                }

                if (!list.Contains(com.Id))
                {
                    list.Add(com.Id);
                }
            }
        }

        foreach (var boundary in boundaries)
        {
            var memberIds = new HashSet<string>(boundary.MemberEntityIds, StringComparer.Ordinal);

            // Candidate target binaries for this boundary: its own members, plus every binary any
            // member IMPORTS — exactly the two target shapes Tier 1/Tier 2 below already check
            // (skill.md §11), never a new association path. Only COMs referencing one of these
            // binaries can possibly produce an association for this boundary.
            var candidateBinaryIds = new HashSet<string>(memberIds, StringComparer.Ordinal);
            foreach (var memberId in boundary.MemberEntityIds)
            {
                foreach (var importEdge in graph.EdgesFrom(memberId).Where(e => e.Type == DependencyEdgeType.Imports))
                {
                    candidateBinaryIds.Add(importEdge.TargetEntityId);
                }
            }

            var candidateComIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binaryId in candidateBinaryIds)
            {
                if (comsByReferencedBinary.TryGetValue(binaryId, out var comIds))
                {
                    candidateComIds.UnionWith(comIds);
                }
            }

            // Evaluate candidates in context.ComComponents' original relative order, and — for
            // each candidate — its own edges in graph.EdgesFrom's original order, breaking on the
            // first tier-1-or-tier-2 match exactly as before. This preserves the exact
            // first-match-wins result/order this method already produced; only which (boundary,
            // COM) pairs get evaluated at all has changed (skill.md §2, §9).
            foreach (var comId in candidateComIds.OrderBy(id => comOrder[id]))
            {
                foreach (var comEdge in graph.EdgesFrom(comId).Where(e => e.Type == DependencyEdgeType.References))
                {
                    if (memberIds.Contains(comEdge.TargetEntityId))
                    {
                        result.Add(new DerivedWorkloadDependency
                        {
                            BoundaryId = boundary.Id,
                            TargetEntityId = comId,
                            Type = DependencyEdgeType.References,
                            Confidence = Confidence.High(),
                            Evidence = comEdge.Evidence,
                            DerivedFrom = $"COM {comId} REFERENCES {comEdge.TargetEntityId}, a member of ApplicationBoundary {boundary.Id}"
                        });
                        diagnostics.RecordDerivedWorkloadDependency();
                        break;
                    }

                    var importingMember = memberIds.FirstOrDefault(m =>
                        graph.EdgesFrom(m).Any(e => e.Type == DependencyEdgeType.Imports && e.TargetEntityId == comEdge.TargetEntityId));

                    if (importingMember is not null)
                    {
                        result.Add(new DerivedWorkloadDependency
                        {
                            BoundaryId = boundary.Id,
                            TargetEntityId = comId,
                            Type = DependencyEdgeType.References,
                            Confidence = Confidence.Medium(),
                            Evidence = comEdge.Evidence,
                            DerivedFrom = $"{importingMember} IMPORTS {comEdge.TargetEntityId}, which COM {comId} REFERENCES"
                        });
                        diagnostics.RecordDerivedWorkloadDependency();
                        break;
                    }
                }
            }
        }

        // Bounded diagnostic: a COM registration whose referenced binary is not a member of ANY
        // boundary is recorded once — not per-boundary — never fabricating a global attachment.
        foreach (var com in context.ComComponents)
        {
            var targets = graph.EdgesFrom(com.Id).Where(e => e.Type == DependencyEdgeType.References).Select(e => e.TargetEntityId).ToList();
            if (targets.Count > 0 && targets.All(t => !allBoundaryMembers.Contains(t)))
            {
                diagnostics.RecordUnresolvedComRelationship(com.Id, "Referenced binary is not a member of any workload boundary");
            }
        }

        return result;
    }

    /// <summary>ApplicationBoundary → ExternalDependency, derived from a member Configuration's
    /// REFERENCES edge — preserves the full provenance chain (skill.md §21-22).</summary>
    private static List<DerivedWorkloadDependency> BuildExternalDependencyWorkloadAssociations(
        IReadOnlyList<ApplicationBoundary> boundaries,
        DependencyGraph expandedGraph,
        ExpansionDiagnostics diagnostics)
    {
        var result = new List<DerivedWorkloadDependency>();

        foreach (var boundary in boundaries)
        {
            foreach (var memberId in boundary.MemberEntityIds)
            {
                foreach (var edge in expandedGraph.EdgesFrom(memberId).Where(e => e.Type == DependencyEdgeType.References))
                {
                    if (!expandedGraph.TryGetNode(edge.TargetEntityId, out var targetNode) || targetNode is not ExternalDependency)
                    {
                        continue;
                    }

                    result.Add(new DerivedWorkloadDependency
                    {
                        BoundaryId = boundary.Id,
                        TargetEntityId = edge.TargetEntityId,
                        Type = DependencyEdgeType.DependsOn,
                        Confidence = new Confidence(Math.Min(boundary.Confidence.Value, edge.Confidence.Value)),
                        Evidence = edge.Evidence,
                        DerivedFrom = $"ApplicationBoundary {boundary.Id} owns Configuration {memberId}, which REFERENCES ExternalDependency {edge.TargetEntityId}"
                    });
                    diagnostics.RecordDerivedWorkloadDependency();
                }
            }
        }

        return result;
    }
}
