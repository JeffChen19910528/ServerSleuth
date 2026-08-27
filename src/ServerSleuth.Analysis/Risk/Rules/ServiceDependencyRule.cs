using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Risk.Rules;

/// <summary>
/// RR6 (skill.md (Phase 7A) §17): a service whose executable dependency is missing or never
/// resolved. Phrased strictly as a migration risk — never as "this service is currently
/// stopped/broken," since discovery makes no live-health claim. When the dependency edge
/// resolved but the target binary's file is confirmed absent, this finding shares the
/// <c>MissingBinaryEntityId</c> merge anchor with <see cref="MissingBinaryRule"/>'s own finding
/// for the same binary (skill.md §25's explicit dedup example).
/// </summary>
public sealed class ServiceDependencyRule : IRiskRule
{
    public string Id => "RR6-ServiceDependency";
    public RiskCategory Category => RiskCategory.Service;
    public RiskSeverity DefaultSeverity => RiskSeverity.Critical;

    public IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context)
    {
        var findings = new List<RiskFinding>();

        foreach (var service in context.Services)
        {
            if (service.ExecutablePath is null)
            {
                continue;
            }

            var runsEdges = context.Graph.EdgesFrom(service.Id).Where(e => e.Type == DependencyEdgeType.Runs).ToList();

            if (runsEdges.Count == 0)
            {
                findings.Add(new RiskFinding
                {
                    Id = RiskFinding.ComputeId(Id, service.Id),
                    RuleId = Id,
                    Category = Category,
                    Severity = DefaultSeverity,
                    Confidence = service.Confidence,
                    Title = $"Service executable dependency unresolved: {service.Name}",
                    Description = $"Service '{service.Name}' references executable '{service.ExecutablePath}', which could not be resolved to any discovered binary entity.",
                    SourceEntityId = service.Id,
                    Evidence = [new EvidenceRecord { Type = EvidenceType.ServiceConfiguration, Location = service.Id, Detail = $"ExecutablePath={service.ExecutablePath}" }],
                    Recommendation = "Confirm the service's executable will be present (or rebuilt) on the target environment before migration."
                });
                continue;
            }

            foreach (var edge in runsEdges)
            {
                if (!context.ById.TryGetValue(edge.TargetEntityId, out var target) || target.Metadata.GetValueOrDefault("FileStatus") != "NotFound")
                {
                    continue;
                }

                var confidence = edge.Confidence.Value < service.Confidence.Value ? edge.Confidence : service.Confidence;

                findings.Add(new RiskFinding
                {
                    Id = RiskFinding.ComputeId(Id, service.Id, [target.Id]),
                    RuleId = Id,
                    Category = Category,
                    Severity = DefaultSeverity,
                    Confidence = confidence,
                    Title = $"Service executable missing on disk: {service.Name}",
                    Description = $"Service '{service.Name}' runs '{target.Name}', but that file was not found on disk during discovery. This service cannot be reproduced on the target environment unless the executable is migrated or rebuilt.",
                    SourceEntityId = service.Id,
                    RelatedEntityIds = [target.Id],
                    Evidence = [new EvidenceRecord { Type = EvidenceType.ServiceConfiguration, Location = service.Id, Detail = $"Runs {target.Id}" }],
                    Recommendation = "Locate and migrate the missing executable, or rebuild it for the target environment before cutover.",
                    Metadata = new Dictionary<string, string> { ["MissingBinaryEntityId"] = target.Id }
                });
            }
        }

        return findings;
    }
}
