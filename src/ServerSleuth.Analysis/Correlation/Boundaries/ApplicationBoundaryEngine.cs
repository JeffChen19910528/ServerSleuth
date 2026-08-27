using ServerSleuth.Analysis.Correlation.Boundaries.Diagnostics;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Correlation.Boundaries;

/// <summary>
/// Answers "which discovered entities appear to belong to the same logical application?" —
/// skill.md §1. Consumes already-discovered entities plus the Phase 5A <see cref="DependencyGraph"/>
/// as evidence (skill.md §18); performs no new discovery, filesystem access, or process
/// execution of any kind (skill.md §25-26).
///
/// Algorithm (skill.md §19), all deterministic — no dictionary-enumeration-order dependence:
///   1. Build one WorkloadAnchor per IIS Application / Service (with resolvable exe) /
///      Scheduled Task (with resolvable exe) — the only three strong identity sources.
///   2. Build one BoundaryCandidate per anchor: members = the anchor itself, plus whatever
///      Phase 5A already connected to it via Contains/Configures/Runs edges, plus any
///      Configuration whose existing OwnerEntityId names this anchor directly (skill.md §6
///      Rule D — this is NOT re-discovering ownership, only reading what Phase 4E-1 already
///      recorded).
///   3. Merge exactly-two-anchor groups that RUN the identical resolved binary (skill.md §7) —
///      never three-or-more (skill.md §8: that's shared/common infrastructure, not identity).
///   4. Record (never merge on) common-parent-directory candidates (skill.md §9) and leave
///      name similarity entirely unused as merge evidence (skill.md §10).
/// </summary>
public sealed class ApplicationBoundaryEngine
{
    public BoundaryAnalysisResult Analyze(IReadOnlyList<DiscoveryEntity> entities, DependencyGraph graph)
    {
        var context = new CorrelationContext(entities);
        var diagnostics = new BoundaryDiagnostics();

        var anchors = BuildAnchors(context, diagnostics);
        var candidates = anchors.Select(anchor => BuildCandidate(anchor, context, graph)).ToList();

        var merged = MergeSharedExecutableWorkloads(candidates, graph, diagnostics);

        RecordCommonParentCandidates(merged, diagnostics);
        RecordUnresolvedOwnership(context, merged, diagnostics);

        foreach (var _ in merged)
        {
            diagnostics.RecordConfirmedBoundary();
        }

        var boundaries = merged.Select(ToApplicationBoundary).ToList();
        return new BoundaryAnalysisResult { Boundaries = boundaries, Diagnostics = diagnostics };
    }

    private static IReadOnlyList<WorkloadAnchor> BuildAnchors(CorrelationContext context, BoundaryDiagnostics diagnostics)
    {
        var anchors = new List<WorkloadAnchor>();

        foreach (var app in context.Applications)
        {
            diagnostics.RecordWorkloadCandidateEvaluated();
            anchors.Add(new WorkloadAnchor
            {
                AnchorEntityId = app.Id,
                Kind = WorkloadAnchorKind.IisApplication,
                Name = app.Name,
                RootPath = app.Path,
                SelfEvidence = new EvidenceRecord { Type = EvidenceType.IisConfiguration, Location = app.Id, Detail = "IIS Application PhysicalPath" }
            });
        }

        foreach (var service in context.Services)
        {
            diagnostics.RecordWorkloadCandidateEvaluated();
            if (service.ExecutablePath is null)
            {
                diagnostics.RecordUnresolvedOwnership(service.Id, "Service has no ExecutablePath — cannot anchor a workload");
                continue;
            }

            var parsed = CommandLineReference.Parse(service.ExecutablePath);
            if (parsed.ExecutablePath is null)
            {
                diagnostics.RecordUnresolvedOwnership(service.Id, $"ImagePath '{service.ExecutablePath}' could not be unambiguously parsed");
                continue;
            }

            var normalized = WindowsPathNormalizer.Normalize(parsed.ExecutablePath);
            anchors.Add(new WorkloadAnchor
            {
                AnchorEntityId = service.Id,
                Kind = WorkloadAnchorKind.Service,
                Name = service.Name,
                RootPath = WindowsPathNormalizer.GetDirectoryName(normalized.Value),
                SelfEvidence = new EvidenceRecord
                {
                    Type = EvidenceType.Registry,
                    Location = $@"HKLM\SYSTEM\CurrentControlSet\Services\{service.Name}",
                    Detail = "Windows Service ImagePath"
                }
            });
        }

        foreach (var task in context.ScheduledTasks)
        {
            diagnostics.RecordWorkloadCandidateEvaluated();
            if (task.Action is null)
            {
                diagnostics.RecordUnresolvedOwnership(task.Id, "Scheduled Task has no action executable — cannot anchor a workload");
                continue;
            }

            var parsed = CommandLineReference.Parse(task.Action);
            if (parsed.ExecutablePath is null)
            {
                diagnostics.RecordUnresolvedOwnership(task.Id, $"Action '{task.Action}' could not be unambiguously parsed");
                continue;
            }

            var normalized = WindowsPathNormalizer.Normalize(parsed.ExecutablePath);
            anchors.Add(new WorkloadAnchor
            {
                AnchorEntityId = task.Id,
                Kind = WorkloadAnchorKind.ScheduledTask,
                Name = task.Name,
                RootPath = WindowsPathNormalizer.GetDirectoryName(normalized.Value),
                SelfEvidence = new EvidenceRecord { Type = EvidenceType.ScheduledTask, Location = task.Id, Detail = "Scheduled Task ExecAction" }
            });
        }

        return anchors;
    }

    private static BoundaryCandidate BuildCandidate(WorkloadAnchor anchor, CorrelationContext context, DependencyGraph graph)
    {
        var members = new List<string> { anchor.AnchorEntityId };
        var evidence = new List<EvidenceRecord> { anchor.SelfEvidence };

        foreach (var edge in graph.EdgesFrom(anchor.AnchorEntityId))
        {
            if (edge.Type is DependencyEdgeType.Contains or DependencyEdgeType.Configures or DependencyEdgeType.Runs)
            {
                if (!members.Contains(edge.TargetEntityId))
                {
                    members.Add(edge.TargetEntityId);
                }

                evidence.AddRange(edge.Evidence);
            }
        }

        // Rule D (skill.md §6): Configuration ownership already recorded by Phase 4E-1's
        // ScanRootCollector — preserved here directly, not re-derived. This covers Service/Task
        // owned configuration files, which Phase 5A's Application-only rule never produced an
        // edge for.
        foreach (var configuration in context.Configurations)
        {
            if (configuration.Metadata.TryGetValue("OwnerEntityId", out var ownerId) &&
                ownerId == anchor.AnchorEntityId &&
                !members.Contains(configuration.Id))
            {
                members.Add(configuration.Id);
                evidence.Add(new EvidenceRecord
                {
                    Type = EvidenceType.ConfigurationFile,
                    Location = configuration.Path ?? configuration.Id,
                    Detail = $"OwnerEntityId={anchor.AnchorEntityId}"
                });
            }
        }

        var reason = anchor.Kind switch
        {
            WorkloadAnchorKind.IisApplication => "IIS Application PhysicalPath root",
            WorkloadAnchorKind.Service => "Windows Service executable",
            WorkloadAnchorKind.ScheduledTask => "Scheduled Task executable",
            _ => "Unknown anchor"
        };

        return new BoundaryCandidate
        {
            Id = $"boundary:{anchor.AnchorEntityId}",
            Name = anchor.Name,
            AnchorEntityIds = [anchor.AnchorEntityId],
            SingleAnchorKind = anchor.Kind,
            MemberEntityIds = members,
            Evidence = evidence,
            Confidence = Confidence.VeryHigh(),
            Reason = reason,
            RootPath = anchor.RootPath
        };
    }

    /// <summary>Merges exactly-two-anchor Service/Task groups sharing an identical RUNS target
    /// (skill.md §7). A shared target referenced by three or more anchors is common/shared
    /// infrastructure, not workload-identifying evidence (skill.md §8), and is recorded as a
    /// diagnostic instead of merged.</summary>
    private static List<BoundaryCandidate> MergeSharedExecutableWorkloads(
        List<BoundaryCandidate> candidates,
        DependencyGraph graph,
        BoundaryDiagnostics diagnostics)
    {
        var runsTargets = new Dictionary<string, List<BoundaryCandidate>>(StringComparer.Ordinal);

        foreach (var candidate in candidates.Where(c => c.SingleAnchorKind is WorkloadAnchorKind.Service or WorkloadAnchorKind.ScheduledTask))
        {
            var anchorId = candidate.AnchorEntityIds[0];
            var runsEdge = graph.EdgesFrom(anchorId).FirstOrDefault(e => e.Type == DependencyEdgeType.Runs);
            if (runsEdge is null)
            {
                continue;
            }

            if (!runsTargets.TryGetValue(runsEdge.TargetEntityId, out var list))
            {
                runsTargets[runsEdge.TargetEntityId] = list = [];
            }

            list.Add(candidate);
        }

        var result = new List<BoundaryCandidate>(candidates);
        var consumed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dllId in runsTargets.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var sharers = runsTargets[dllId].OrderBy(c => c.AnchorEntityIds[0], StringComparer.Ordinal).ToList();

            if (sharers.Count < 2)
            {
                continue;
            }

            if (sharers.Count > 2)
            {
                diagnostics.RecordSharedBinary(
                    dllId,
                    sharers.Select(s => s.AnchorEntityIds[0]).ToList(),
                    $"Shared by {sharers.Count} workload anchors — too common to treat as workload-identifying evidence (skill.md §8)");
                continue;
            }

            var a = sharers[0];
            var b = sharers[1];

            if (consumed.Contains(a.AnchorEntityIds[0]) || consumed.Contains(b.AnchorEntityIds[0]))
            {
                diagnostics.RecordRejectedMerge(
                    [a.AnchorEntityIds[0], b.AnchorEntityIds[0]],
                    "One of the anchors is already part of a different merged boundary");
                continue;
            }

            result.Remove(a);
            result.Remove(b);
            result.Add(MergeTwo(a, b, dllId));
            consumed.Add(a.AnchorEntityIds[0]);
            consumed.Add(b.AnchorEntityIds[0]);
            diagnostics.RecordMerge();
        }

        return result;
    }

    private static BoundaryCandidate MergeTwo(BoundaryCandidate a, BoundaryCandidate b, string sharedDllId)
    {
        var anchorIds = new[] { a.AnchorEntityIds[0], b.AnchorEntityIds[0] }.OrderBy(id => id, StringComparer.Ordinal).ToList();
        var id = $"boundary:{string.Join("+", anchorIds)}";

        // Prefer the Service's name for the merged workload — matches skill.md §15's example
        // ("ERPWorker" workload named after the service, not the task).
        var name = a.SingleAnchorKind == WorkloadAnchorKind.Service ? a.Name
            : b.SingleAnchorKind == WorkloadAnchorKind.Service ? b.Name
            : anchorIds[0];

        var members = a.MemberEntityIds.Concat(b.MemberEntityIds).Distinct().ToList();
        var evidence = a.Evidence.Concat(b.Evidence).ToList();
        evidence.Add(new EvidenceRecord
        {
            Type = EvidenceType.PeMetadata,
            Location = sharedDllId,
            Detail = $"Shared execution target between {a.AnchorEntityIds[0]} and {b.AnchorEntityIds[0]}"
        });

        return new BoundaryCandidate
        {
            Id = id,
            Name = name,
            AnchorEntityIds = anchorIds,
            SingleAnchorKind = null,
            MemberEntityIds = members,
            Evidence = evidence,
            Confidence = Confidence.High(),
            Reason = $"Shared execution target between {a.AnchorEntityIds[0]} and {b.AnchorEntityIds[0]}",
            RootPath = a.RootPath ?? b.RootPath
        };
    }

    /// <summary>Common parent directory is weak evidence only (skill.md §9) — recorded as a
    /// diagnostic candidate, never merged.</summary>
    private static void RecordCommonParentCandidates(IReadOnlyList<BoundaryCandidate> boundaries, BoundaryDiagnostics diagnostics)
    {
        var rooted = boundaries
            .Select(b => (Boundary: b, Parent: b.RootPath is null ? null : WindowsPathNormalizer.GetDirectoryName(WindowsPathNormalizer.Normalize(b.RootPath).Value)))
            .Where(x => x.Parent is not null)
            .ToList();

        for (var i = 0; i < rooted.Count; i++)
        {
            for (var j = i + 1; j < rooted.Count; j++)
            {
                if (!string.Equals(rooted[i].Parent, rooted[j].Parent, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                diagnostics.RecordAmbiguousCandidate(
                    [rooted[i].Boundary.Id, rooted[j].Boundary.Id],
                    $"Both roots share common parent directory '{rooted[i].Parent}' — common-parent is weak evidence only, not merged (skill.md §9)");
            }
        }
    }

    private static void RecordUnresolvedOwnership(CorrelationContext context, IReadOnlyList<BoundaryCandidate> boundaries, BoundaryDiagnostics diagnostics)
    {
        var claimedIds = new HashSet<string>(boundaries.SelectMany(b => b.MemberEntityIds), StringComparer.Ordinal);

        foreach (var dll in context.Dlls)
        {
            if (!claimedIds.Contains(dll.Id))
            {
                diagnostics.RecordUnresolvedOwnership(dll.Id, "No IIS Application/Service/Scheduled Task anchor claims this binary");
            }
        }

        foreach (var configuration in context.Configurations)
        {
            if (!claimedIds.Contains(configuration.Id))
            {
                diagnostics.RecordUnresolvedOwnership(configuration.Id, "OwnerEntityId did not resolve to a known workload anchor");
            }
        }
    }

    private static ApplicationBoundary ToApplicationBoundary(BoundaryCandidate candidate) => new()
    {
        Id = candidate.Id,
        Name = candidate.Name,
        MemberEntityIds = candidate.MemberEntityIds,
        Evidence = candidate.Evidence,
        Confidence = candidate.Confidence,
        Reason = candidate.Reason
    };
}
