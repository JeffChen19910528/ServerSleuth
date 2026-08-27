using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Correlation.Rules;

/// <summary>Rule 1 (skill.md §14): IIS Site --HOSTS--> IIS Application. Identity comes from the
/// Application's existing ComponentEntityIds (populated by IisScanner in Phase 4A) rather than
/// re-deriving the relationship from names or paths.</summary>
public sealed class SiteHostsApplicationRule : ICorrelationRule
{
    public string Id => "R1-SiteHostsApplication";

    public IReadOnlyList<CorrelationCandidate> Evaluate(CorrelationContext context)
    {
        var candidates = new List<CorrelationCandidate>();

        foreach (var application in context.Applications)
        {
            foreach (var componentId in application.ComponentEntityIds)
            {
                if (!context.ById.TryGetValue(componentId, out var component) || component is not WebSite site)
                {
                    continue;
                }

                candidates.Add(new CorrelationCandidate
                {
                    RuleId = Id,
                    SourceEntityId = site.Id,
                    TargetEntityId = application.Id,
                    Type = DependencyEdgeType.Hosts,
                    Confidence = Confidence.VeryHigh(),
                    Evidence =
                    [
                        new EvidenceRecord
                        {
                            Type = EvidenceType.IisConfiguration,
                            Location = application.Id,
                            Detail = $"Application.ComponentEntityIds references Site {site.Id}"
                        }
                    ]
                });
            }
        }

        return candidates;
    }
}
