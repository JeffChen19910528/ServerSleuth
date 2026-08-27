using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Risk.Rules;

/// <summary>
/// RR8 (skill.md (Phase 7A) §19): a COM registration whose server binary is missing or never
/// resolved. Severity is High by default; it only escalates to Critical when there is explicit
/// evidence — membership in an <see cref="ServerSleuth.Core.Boundaries.ApplicationBoundary"/> —
/// that some application actually depends on this COM registration. Naming similarity is never
/// used to infer application ownership (skill.md §19's explicit prohibition); only the
/// already-established boundary membership index is consulted.
/// </summary>
public sealed class ComDependencyRule : IRiskRule
{
    public string Id => "RR8-ComDependency";
    public RiskCategory Category => RiskCategory.Com;
    public RiskSeverity DefaultSeverity => RiskSeverity.High;

    public IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context)
    {
        var findings = new List<RiskFinding>();

        foreach (var com in context.ComComponents)
        {
            if (com.InprocServer32 is null && com.LocalServer32 is null)
            {
                continue; // no server reference at all — nothing to evaluate
            }

            var isBoundaryMember = context.BoundaryIdByEntityId.TryGetValue(com.Id, out var boundaryId);
            var severity = isBoundaryMember ? RiskSeverity.Critical : DefaultSeverity;

            var referencesEdges = context.Graph.EdgesFrom(com.Id).Where(e => e.Type == DependencyEdgeType.References).ToList();

            if (referencesEdges.Count == 0)
            {
                findings.Add(new RiskFinding
                {
                    Id = RiskFinding.ComputeId(Id, com.Id),
                    RuleId = Id,
                    Category = Category,
                    Severity = severity,
                    Confidence = com.Confidence,
                    Title = $"COM server dependency unresolved: {com.ProgId ?? com.Clsid}",
                    Description = $"COM registration '{com.ProgId ?? com.Clsid}' references a server path that could not be resolved to any discovered binary entity.",
                    SourceEntityId = com.Id,
                    ApplicationBoundaryId = boundaryId,
                    Evidence = [new EvidenceRecord { Type = EvidenceType.Registry, Location = com.Id, Detail = $"InprocServer32={com.InprocServer32};LocalServer32={com.LocalServer32}" }],
                    Recommendation = "Confirm the COM server binary will be present (or re-registered) on the target environment before migration."
                });
                continue;
            }

            foreach (var edge in referencesEdges)
            {
                if (!context.ById.TryGetValue(edge.TargetEntityId, out var target) || target.Metadata.GetValueOrDefault("FileStatus") != "NotFound")
                {
                    continue;
                }

                var confidence = edge.Confidence.Value < com.Confidence.Value ? edge.Confidence : com.Confidence;

                findings.Add(new RiskFinding
                {
                    Id = RiskFinding.ComputeId(Id, com.Id, [target.Id]),
                    RuleId = Id,
                    Category = Category,
                    Severity = severity,
                    Confidence = confidence,
                    Title = $"COM server binary missing on disk: {com.ProgId ?? com.Clsid}",
                    Description = $"COM registration '{com.ProgId ?? com.Clsid}' references '{target.Name}', but that file was not found on disk during discovery.",
                    SourceEntityId = com.Id,
                    RelatedEntityIds = [target.Id],
                    ApplicationBoundaryId = boundaryId,
                    Evidence = [new EvidenceRecord { Type = EvidenceType.Registry, Location = com.Id, Detail = $"References {target.Id}" }],
                    Recommendation = "Locate and migrate the missing COM server binary, or re-register an equivalent component on the target environment.",
                    Metadata = new Dictionary<string, string> { ["MissingBinaryEntityId"] = target.Id }
                });
            }
        }

        return findings;
    }
}
