using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Correlation.Validation;

/// <summary>
/// Validates the structural, semantic, provenance, and evidence-quality integrity of an
/// already-built graph — see skill.md (Phase 5D) §1. Read-only by construction: it only ever
/// reads <see cref="DependencyGraph.Nodes"/>/<see cref="DependencyGraph.Edges"/> through the
/// graph's own public accessors and never calls <see cref="DependencyGraph.AddNode"/>/
/// <see cref="DependencyGraph.AddEdge"/> (skill.md §24). Findings are diagnostics, not repairs —
/// this class never mutates its input.
/// </summary>
public sealed class GraphValidator
{
    public GraphValidationResult Validate(
        IReadOnlyList<DiscoveryEntity> entities,
        DependencyExpansionResult expansion,
        IReadOnlyList<ApplicationBoundary> boundaries)
    {
        var graph = expansion.ExpandedGraph;
        var byId = new Dictionary<string, DiscoveryEntity>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            byId.TryAdd(node.Id, node);
        }

        var findings = new List<ValidationFinding>();

        ValidateNodeIntegrity(entities, findings);
        ValidateEdgeIntegrity(graph, byId, findings);
        var duplicateEdgeCount = ValidateDuplicateEdges(graph, findings);
        ValidateEvidenceValidity(graph, byId, findings);
        ValidateProvenance(expansion.DerivedWorkloadDependencies, boundaries, byId, findings);
        ValidateConfidenceConsistency(graph, expansion.DerivedWorkloadDependencies, findings);
        var selfEdgeCount = ValidateSelfEdges(graph, findings);
        ValidateUnresolvedDependencies(graph, byId, findings);
        ValidateExternalDependencies(expansion.ExternalDependencies, findings);
        ValidateCertificates(graph, byId, findings);
        ValidateComComponents(graph, byId, findings);
        ValidateRuntimeEdges(graph, findings);

        var orphans = FindOrphans(graph);
        var cycles = FindCycles(graph);

        var missingEvidenceCount = findings.Count(f => f.Code == "MissingEvidence");
        var danglingCount = findings.Count(f => f.Code is "DanglingSource" or "DanglingTarget");
        var confidenceIssueCount = findings.Count(f => f.Category == "ConfidenceConsistency");
        var invalidEdgeCount = findings.Count(f => f.Category == "EdgeIntegrity" && f.Severity == ValidationSeverity.Error);

        var summary = new GraphValidationSummary
        {
            TotalNodes = graph.Nodes.Count,
            TotalEdges = graph.Edges.Count,
            ValidEdges = graph.Edges.Count - invalidEdgeCount,
            InvalidEdges = invalidEdgeCount,
            DuplicateEdges = duplicateEdgeCount,
            MissingEvidence = missingEvidenceCount,
            DanglingEdges = danglingCount,
            Cycles = cycles.Count,
            Orphans = orphans.Count,
            UnresolvedDependencies = findings.Count(f => f.Category == "UnresolvedDependency"),
            ConfidenceIssues = confidenceIssueCount
        };

        return new GraphValidationResult
        {
            Findings = SortFindings(findings),
            Orphans = orphans.OrderBy(o => o.EntityId, StringComparer.Ordinal).ToList(),
            Cycles = cycles.OrderBy(c => c.CycleId, StringComparer.Ordinal).ToList(),
            Summary = summary
        };
    }

    private static IReadOnlyList<ValidationFinding> SortFindings(List<ValidationFinding> findings) =>
        findings
            .OrderBy(f => f.Category, StringComparer.Ordinal)
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ThenBy(f => f.EntityIds.Count > 0 ? f.EntityIds[0] : string.Empty, StringComparer.Ordinal)
            .ToList();

    // ---- 3. Node Integrity ----------------------------------------------------------------

    private static void ValidateNodeIntegrity(IReadOnlyList<DiscoveryEntity> entities, List<ValidationFinding> findings)
    {
        foreach (var entity in entities)
        {
            if (string.IsNullOrWhiteSpace(entity.Id))
            {
                findings.Add(new ValidationFinding
                {
                    Category = "NodeIntegrity",
                    Code = "EmptyNodeId",
                    Severity = ValidationSeverity.Error,
                    Message = $"Entity of type '{entity.Type}' has an empty or whitespace Id.",
                    EntityIds = [entity.Id ?? string.Empty]
                });
            }

            if (string.IsNullOrWhiteSpace(entity.Type))
            {
                findings.Add(new ValidationFinding
                {
                    Category = "NodeIntegrity",
                    Code = "InvalidEntityType",
                    Severity = ValidationSeverity.Error,
                    Message = $"Entity '{entity.Id}' has an empty or whitespace Type.",
                    EntityIds = [entity.Id ?? string.Empty]
                });
            }
        }

        // A duplicate Id in the raw entity list is undetectable once inside DependencyGraph
        // (its node dictionary silently keeps only the first occurrence) — this check exists
        // specifically to catch that silent loss before it happens, per skill.md §3.
        foreach (var group in entities.GroupBy(e => e.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            findings.Add(new ValidationFinding
            {
                Category = "NodeIntegrity",
                Code = "DuplicateNodeId",
                Severity = ValidationSeverity.Error,
                Message = $"Id '{group.Key}' appears {group.Count()} times in the discovery input — only the first was kept as a graph node.",
                EntityIds = [group.Key]
            });
        }
    }

    // ---- 4. Edge Integrity -----------------------------------------------------------------

    private static void ValidateEdgeIntegrity(DependencyGraph graph, Dictionary<string, DiscoveryEntity> byId, List<ValidationFinding> findings)
    {
        foreach (var edge in graph.Edges)
        {
            if (string.IsNullOrWhiteSpace(edge.SourceEntityId))
            {
                findings.Add(Finding("EdgeIntegrity", "EmptySourceId", ValidationSeverity.Error,
                    "An edge has an empty SourceEntityId.", []));
                continue;
            }

            if (string.IsNullOrWhiteSpace(edge.TargetEntityId))
            {
                findings.Add(Finding("EdgeIntegrity", "EmptyTargetId", ValidationSeverity.Error,
                    "An edge has an empty TargetEntityId.", [edge.SourceEntityId]));
                continue;
            }

            if (!byId.ContainsKey(edge.SourceEntityId))
            {
                findings.Add(Finding("EdgeIntegrity", "DanglingSource", ValidationSeverity.Error,
                    $"Edge source '{edge.SourceEntityId}' does not exist as a graph node.", [edge.SourceEntityId, edge.TargetEntityId]));
            }

            if (!byId.ContainsKey(edge.TargetEntityId))
            {
                findings.Add(Finding("EdgeIntegrity", "DanglingTarget", ValidationSeverity.Error,
                    $"Edge target '{edge.TargetEntityId}' does not exist as a graph node.", [edge.SourceEntityId, edge.TargetEntityId]));
            }

            if (!Enum.IsDefined(edge.Type))
            {
                findings.Add(Finding("EdgeIntegrity", "InvalidRelationshipType", ValidationSeverity.Error,
                    $"Edge {edge.SourceEntityId} -> {edge.TargetEntityId} has an undefined relationship type value.", [edge.SourceEntityId, edge.TargetEntityId]));
            }

            if (edge.Evidence.Count == 0)
            {
                findings.Add(Finding("EdgeIntegrity", "MissingEvidence", ValidationSeverity.Error,
                    $"Edge {edge.SourceEntityId} --{edge.Type}--> {edge.TargetEntityId} has no evidence.", [edge.SourceEntityId, edge.TargetEntityId]));
            }
        }
    }

    // ---- 5. Duplicate Edge Detection --------------------------------------------------------

    private static int ValidateDuplicateEdges(DependencyGraph graph, List<ValidationFinding> findings)
    {
        var groups = graph.Edges
            .GroupBy(e => (e.SourceEntityId, e.TargetEntityId, e.Type))
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in groups)
        {
            findings.Add(Finding("DuplicateDetection", "DuplicateEdge", ValidationSeverity.Error,
                $"{group.Count()} edges share the identical Source/Target/Type triple {group.Key.SourceEntityId} --{group.Key.Type}--> {group.Key.TargetEntityId} — Phase 5A/5C's merge-on-add should have prevented this.",
                [group.Key.SourceEntityId, group.Key.TargetEntityId]));
        }

        return groups.Count;
    }

    // ---- 7. Evidence Validity ---------------------------------------------------------------

    private static void ValidateEvidenceValidity(DependencyGraph graph, Dictionary<string, DiscoveryEntity> byId, List<ValidationFinding> findings)
    {
        foreach (var edge in graph.Edges)
        {
            if (edge.Evidence.Count == 0 || !byId.TryGetValue(edge.SourceEntityId, out var source))
            {
                continue; // already reported by ValidateEdgeIntegrity
            }

            EvidenceType? expected = (source, edge.Type) switch
            {
                (ComComponent, DependencyEdgeType.References) => EvidenceType.Registry,
                (Service, DependencyEdgeType.Runs) => EvidenceType.Registry,
                (ScheduledTask, DependencyEdgeType.Runs) => EvidenceType.ScheduledTask,
                (WebSite, DependencyEdgeType.Binds) => EvidenceType.IisConfiguration,
                (Dll, DependencyEdgeType.Imports) => EvidenceType.PeMetadata,
                (Configuration, DependencyEdgeType.References) => EvidenceType.ConfigurationFile,
                _ => null
            };

            if (expected is not null && !edge.Evidence.Any(e => e.Type == expected))
            {
                findings.Add(Finding("EvidenceCompleteness", "InvalidEvidenceType", ValidationSeverity.Warning,
                    $"Edge {edge.SourceEntityId} --{edge.Type}--> {edge.TargetEntityId} has evidence, but none of type {expected} as expected for this relationship shape.",
                    [edge.SourceEntityId, edge.TargetEntityId]));
            }
        }
    }

    // ---- 8. Provenance Integrity + 9. Confidence Escalation --------------------------------

    private static void ValidateProvenance(
        IReadOnlyList<DerivedWorkloadDependency> derived,
        IReadOnlyList<ApplicationBoundary> boundaries,
        Dictionary<string, DiscoveryEntity> byId,
        List<ValidationFinding> findings)
    {
        var boundariesById = boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);

        foreach (var dependency in derived)
        {
            if (string.IsNullOrWhiteSpace(dependency.DerivedFrom))
            {
                findings.Add(Finding("ProvenanceIntegrity", "EmptyProvenance", ValidationSeverity.Error,
                    $"Derived dependency {dependency.BoundaryId} -> {dependency.TargetEntityId} has no DerivedFrom provenance.",
                    [dependency.BoundaryId, dependency.TargetEntityId]));
            }

            if (dependency.Evidence.Count == 0)
            {
                findings.Add(Finding("ProvenanceIntegrity", "MissingProvenanceEvidence", ValidationSeverity.Error,
                    $"Derived dependency {dependency.BoundaryId} -> {dependency.TargetEntityId} has no evidence.",
                    [dependency.BoundaryId, dependency.TargetEntityId]));
            }

            if (!boundariesById.TryGetValue(dependency.BoundaryId, out var boundary))
            {
                findings.Add(Finding("ProvenanceIntegrity", "UnresolvedProvenanceBoundary", ValidationSeverity.Error,
                    $"Derived dependency references boundary '{dependency.BoundaryId}', which is not in the supplied boundary list.",
                    [dependency.BoundaryId]));
                continue;
            }

            if (!byId.ContainsKey(dependency.TargetEntityId))
            {
                findings.Add(Finding("ProvenanceIntegrity", "UnresolvedProvenanceTarget", ValidationSeverity.Error,
                    $"Derived dependency's target '{dependency.TargetEntityId}' does not exist as a graph node.",
                    [dependency.BoundaryId, dependency.TargetEntityId]));
            }

            // skill.md §9: a derived relationship must never claim stronger evidence than its
            // source. The boundary's own confidence is always one of the two inputs Phase 5C's
            // "weakest link" rule used to compute dependency.Confidence, so it can never
            // legitimately be exceeded.
            if (dependency.Confidence.Value > boundary.Confidence.Value)
            {
                findings.Add(Finding("ProvenanceIntegrity", "ConfidenceEscalation", ValidationSeverity.Error,
                    $"Derived dependency {dependency.BoundaryId} -> {dependency.TargetEntityId} has confidence {dependency.Confidence.Value:0.00}, exceeding its own boundary's confidence {boundary.Confidence.Value:0.00}.",
                    [dependency.BoundaryId, dependency.TargetEntityId]));
            }
        }
    }

    // ---- 10. Confidence Consistency ---------------------------------------------------------

    private static void ValidateConfidenceConsistency(DependencyGraph graph, IReadOnlyList<DerivedWorkloadDependency> derived, List<ValidationFinding> findings)
    {
        foreach (var edge in graph.Edges)
        {
            if (edge.Evidence.Count == 0 && edge.Confidence.Band != ConfidenceBand.VeryLow)
            {
                findings.Add(Finding("ConfidenceConsistency", "ConfidenceWithoutEvidence", ValidationSeverity.Error,
                    $"Edge {edge.SourceEntityId} --{edge.Type}--> {edge.TargetEntityId} claims {edge.Confidence.Band} confidence with zero evidence.",
                    [edge.SourceEntityId, edge.TargetEntityId]));
            }
        }

        foreach (var dependency in derived)
        {
            if (dependency.Evidence.Count == 0 && dependency.Confidence.Band != ConfidenceBand.VeryLow)
            {
                findings.Add(Finding("ConfidenceConsistency", "ConfidenceWithoutEvidence", ValidationSeverity.Error,
                    $"Derived dependency {dependency.BoundaryId} -> {dependency.TargetEntityId} claims {dependency.Confidence.Band} confidence with zero evidence.",
                    [dependency.BoundaryId, dependency.TargetEntityId]));
            }
        }
    }

    // ---- 11. Self-Edge Detection -------------------------------------------------------------

    private static int ValidateSelfEdges(DependencyGraph graph, List<ValidationFinding> findings)
    {
        var count = 0;
        foreach (var edge in graph.Edges.Where(e => e.SourceEntityId == e.TargetEntityId))
        {
            count++;
            var code = edge.Type == DependencyEdgeType.DependsOn ? "PotentialLegitimateSelfReference" : "InvalidSelfEdge";
            var severity = code == "InvalidSelfEdge" ? ValidationSeverity.Error : ValidationSeverity.Warning;

            findings.Add(Finding("SelfEdge", code, severity,
                $"Entity '{edge.SourceEntityId}' has a self-edge of type {edge.Type}.",
                [edge.SourceEntityId]));
        }

        return count;
    }

    // ---- 14. Orphan Analysis -----------------------------------------------------------------

    private static List<OrphanFinding> FindOrphans(DependencyGraph graph)
    {
        var orphans = new List<OrphanFinding>();

        foreach (var node in graph.Nodes)
        {
            if (graph.EdgesFrom(node.Id).Any() || graph.EdgesTo(node.Id).Any())
            {
                continue;
            }

            var (classification, reason) = node switch
            {
                ComComponent => (OrphanClassification.Expected, "COM registrations with no application evidence are common and expected."),
                Runtime => (OrphanClassification.Expected, "An installed runtime with no configuration reference is a normal finding."),
                Certificate => (OrphanClassification.Expected, "An unused certificate in the store is a normal finding."),
                Dll or Configuration or ExternalDependency => (OrphanClassification.Potential, $"A discovered {node.Type} with no owner is plausible but worth reviewing."),
                _ => (OrphanClassification.Unresolved, $"A {node.Type} entity is expected to normally participate in at least one relationship.")
            };

            orphans.Add(new OrphanFinding { EntityId = node.Id, EntityType = node.Type, Classification = classification, Reason = reason });
        }

        return orphans;
    }

    // ---- 12-13. Cycle Detection (iterative Tarjan's SCC) --------------------------------------

    private static List<CycleFinding> FindCycles(DependencyGraph graph)
    {
        var adjacency = new Dictionary<string, List<DependencyEdge>>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges.Where(e => e.SourceEntityId != e.TargetEntityId))
        {
            if (!adjacency.TryGetValue(edge.SourceEntityId, out var list))
            {
                adjacency[edge.SourceEntityId] = list = [];
            }

            list.Add(edge);
        }

        var nodeIds = graph.Nodes.Select(n => n.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var sccs = TarjanStronglyConnectedComponents(nodeIds, adjacency);

        var cycles = new List<CycleFinding>();
        var cycleIndex = 0;

        foreach (var component in sccs.Where(c => c.Count > 1))
        {
            var componentSet = new HashSet<string>(component, StringComparer.Ordinal);
            var edgesInCycle = adjacency
                .Where(kvp => componentSet.Contains(kvp.Key))
                .SelectMany(kvp => kvp.Value.Where(e => componentSet.Contains(e.TargetEntityId)))
                .ToList();

            var relationshipTypes = edgesInCycle.Select(e => e.Type).Distinct().OrderBy(t => t.ToString(), StringComparer.Ordinal).ToList();
            var strongTypes = new HashSet<DependencyEdgeType> { DependencyEdgeType.Runs, DependencyEdgeType.Imports, DependencyEdgeType.DependsOn };
            var classification = relationshipTypes.Any(strongTypes.Contains) ? CycleClassification.Strong : CycleClassification.Weak;

            var sortedNodes = component.OrderBy(id => id, StringComparer.Ordinal).ToList();

            cycles.Add(new CycleFinding
            {
                CycleId = $"cycle:{cycleIndex++}:{string.Join(",", sortedNodes)}",
                NodeIds = sortedNodes,
                EdgeDescriptions = edgesInCycle
                    .Select(e => $"{e.SourceEntityId} --{e.Type}--> {e.TargetEntityId}")
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToList(),
                RelationshipTypes = relationshipTypes,
                Classification = classification
            });
        }

        return cycles;
    }

    private static List<List<string>> TarjanStronglyConnectedComponents(List<string> nodeIds, Dictionary<string, List<DependencyEdge>> adjacency)
    {
        var index = 0;
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowLinks = new Dictionary<string, int>(StringComparer.Ordinal);
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var result = new List<List<string>>();

        foreach (var start in nodeIds)
        {
            if (indices.ContainsKey(start))
            {
                continue;
            }

            // Iterative Tarjan using an explicit work stack (frame = node + next-child-index)
            // so a long dependency chain never risks a real call-stack overflow.
            var callStack = new Stack<(string Node, int ChildIndex)>();
            callStack.Push((start, 0));
            indices[start] = lowLinks[start] = index++;
            stack.Push(start);
            onStack.Add(start);

            while (callStack.Count > 0)
            {
                var (node, childIndex) = callStack.Pop();
                var neighbors = adjacency.TryGetValue(node, out var edges) ? edges.Select(e => e.TargetEntityId).ToList() : [];

                if (childIndex < neighbors.Count)
                {
                    callStack.Push((node, childIndex + 1));
                    var next = neighbors[childIndex];

                    if (!indices.ContainsKey(next))
                    {
                        indices[next] = lowLinks[next] = index++;
                        stack.Push(next);
                        onStack.Add(next);
                        callStack.Push((next, 0));
                    }
                    else if (onStack.Contains(next))
                    {
                        lowLinks[node] = Math.Min(lowLinks[node], indices[next]);
                    }

                    continue;
                }

                if (callStack.Count > 0)
                {
                    var parent = callStack.Peek().Node;
                    lowLinks[parent] = Math.Min(lowLinks[parent], lowLinks[node]);
                }

                if (lowLinks[node] == indices[node])
                {
                    var component = new List<string>();
                    string popped;
                    do
                    {
                        popped = stack.Pop();
                        onStack.Remove(popped);
                        component.Add(popped);
                    } while (popped != node);

                    result.Add(component);
                }
            }
        }

        return result;
    }

    // ---- 15-16. Unresolved Dependency Analysis (MissingBinary vs UnresolvedBinary) ----------

    private static void ValidateUnresolvedDependencies(DependencyGraph graph, Dictionary<string, DiscoveryEntity> byId, List<ValidationFinding> findings)
    {
        foreach (var dll in graph.Nodes.OfType<Dll>())
        {
            if (dll.Metadata.GetValueOrDefault("FileStatus") == "NotFound")
            {
                findings.Add(Finding("UnresolvedDependency", "MissingBinary", ValidationSeverity.Warning,
                    $"Binary '{dll.Id}' was referenced but the file does not exist on disk.",
                    [dll.Id]));
            }

            var importsRaw = dll.Metadata.GetValueOrDefault("Imports");
            if (string.IsNullOrEmpty(importsRaw))
            {
                continue;
            }

            var resolvedImportTargets = new HashSet<string>(
                graph.EdgesFrom(dll.Id).Where(e => e.Type == DependencyEdgeType.Imports).Select(e => e.TargetEntityId),
                StringComparer.Ordinal);
            var resolvedImportNames = resolvedImportTargets
                .Select(id => byId.TryGetValue(id, out var target) ? target.Name : null)
                .Where(n => n is not null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var importName in importsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!resolvedImportNames.Contains(importName))
                {
                    findings.Add(Finding("UnresolvedDependency", "UnresolvedBinary", ValidationSeverity.Info,
                        $"'{dll.Id}' imports '{importName}', which does not resolve to any discovered binary entity.",
                        [dll.Id]));
                }
            }
        }
    }

    // ---- 17. ExternalDependency Validation ---------------------------------------------------

    private static void ValidateExternalDependencies(IReadOnlyList<ExternalDependency> dependencies, List<ValidationFinding> findings)
    {
        foreach (var group in dependencies.GroupBy(d => d.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            findings.Add(Finding("ExternalDependency", "DuplicateExternalDependencyId", ValidationSeverity.Error,
                $"ExternalDependency id '{group.Key}' appears {group.Count()} times.",
                [group.Key]));
        }

        var byNormalizedIdentity = dependencies
            .GroupBy(d => (d.Kind, Host: d.Metadata.GetValueOrDefault("Host")?.ToLowerInvariant(), d.Metadata.GetValueOrDefault("Port"), d.Metadata.GetValueOrDefault("Database")))
            .Where(g => g.Select(d => d.Id).Distinct().Count() > 1);

        foreach (var group in byNormalizedIdentity)
        {
            findings.Add(Finding("ExternalDependency", "ExternalDependencyIdentityConflict", ValidationSeverity.Warning,
                $"{group.Count()} ExternalDependency entities share the same normalized Kind/Host/Port/Database but have different Ids: {string.Join(", ", group.Select(d => d.Id))}.",
                group.Select(d => d.Id).ToList()));
        }
    }

    // ---- 18. Certificate Validation -----------------------------------------------------------

    private static void ValidateCertificates(DependencyGraph graph, Dictionary<string, DiscoveryEntity> byId, List<ValidationFinding> findings)
    {
        foreach (var site in graph.Nodes.OfType<WebSite>())
        {
            var index = 0;
            while (site.Metadata.TryGetValue($"Binding{index}.CertificateThumbprint", out var thumbprint))
            {
                var normalized = thumbprint.Replace(" ", string.Empty).Trim().ToUpperInvariant();
                var boundCertIds = graph.EdgesFrom(site.Id)
                    .Where(e => e.Type == DependencyEdgeType.Binds)
                    .Select(e => e.TargetEntityId)
                    .Where(id => byId.TryGetValue(id, out var target) && target is Certificate cert && cert.Thumbprint == normalized)
                    .ToList();

                if (boundCertIds.Count == 0)
                {
                    findings.Add(Finding("Certificate", "UnresolvedCertificate", ValidationSeverity.Warning,
                        $"Site '{site.Id}' binding {index} references thumbprint '{normalized}', which does not resolve to any discovered certificate.",
                        [site.Id]));
                }

                index++;
            }
        }
    }

    // ---- 19. COM Validation --------------------------------------------------------------------

    private static void ValidateComComponents(DependencyGraph graph, Dictionary<string, DiscoveryEntity> byId, List<ValidationFinding> findings)
    {
        foreach (var com in graph.Nodes.OfType<ComComponent>())
        {
            var referencesEdges = graph.EdgesFrom(com.Id).Where(e => e.Type == DependencyEdgeType.References).ToList();

            if (com.InprocServer32 is not null || com.LocalServer32 is not null)
            {
                if (referencesEdges.Count == 0)
                {
                    findings.Add(Finding("Com", "UnresolvedComReference", ValidationSeverity.Warning,
                        $"COM '{com.Id}' has a server reference but no REFERENCES edge was ever established.",
                        [com.Id]));
                }
            }

            foreach (var edge in referencesEdges)
            {
                if (byId.TryGetValue(edge.TargetEntityId, out var target) && target is Dll dll && dll.Metadata.GetValueOrDefault("FileStatus") == "NotFound")
                {
                    findings.Add(Finding("Com", "ComReferencesMissingFile", ValidationSeverity.Warning,
                        $"COM '{com.Id}' REFERENCES '{dll.Id}', which does not exist on disk.",
                        [com.Id, dll.Id]));
                }
            }
        }
    }

    // ---- 20. Runtime Validation -----------------------------------------------------------------

    private static void ValidateRuntimeEdges(DependencyGraph graph, List<ValidationFinding> findings)
    {
        foreach (var edge in graph.Edges.Where(e => e.Type == DependencyEdgeType.References && e.Confidence.Band == ConfidenceBand.High))
        {
            var detail = edge.Evidence.FirstOrDefault(e => e.Detail is not null && e.Detail.Contains("TargetFramework="))?.Detail;
            if (detail is null)
            {
                continue; // not a version-specific runtime edge
            }

            var tfmPart = detail.Split(' ')[0]; // "TargetFramework=net8.0"
            var tfm = tfmPart.Contains('=') ? tfmPart[(tfmPart.IndexOf('=') + 1)..] : null;
            if (tfm is null || !tfm.StartsWith("net", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var majorDigits = new string(tfm[3..].TakeWhile(c => char.IsDigit(c)).ToArray());
            if (majorDigits.Length == 0 || !detail.Contains($"installed runtime version {majorDigits}."))
            {
                findings.Add(Finding("Runtime", "RuntimeMismatch", ValidationSeverity.Error,
                    $"Edge {edge.SourceEntityId} --References--> {edge.TargetEntityId} claims a TargetFramework match ('{detail}') that does not internally agree on major version.",
                    [edge.SourceEntityId, edge.TargetEntityId]));
            }
        }
    }

    private static ValidationFinding Finding(string category, string code, ValidationSeverity severity, string message, IReadOnlyList<string> entityIds) =>
        new() { Category = category, Code = code, Severity = severity, Message = message, EntityIds = entityIds };
}
