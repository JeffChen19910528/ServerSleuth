using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Correlation.Rules;

/// <summary>Rule 2 (skill.md §14): IIS Application --USES--> Application Pool.</summary>
public sealed class ApplicationUsesApplicationPoolRule : ICorrelationRule
{
    public string Id => "R2-ApplicationUsesApplicationPool";

    public IReadOnlyList<CorrelationCandidate> Evaluate(CorrelationContext context)
    {
        var candidates = new List<CorrelationCandidate>();

        foreach (var application in context.Applications)
        {
            foreach (var componentId in application.ComponentEntityIds)
            {
                if (!context.ById.TryGetValue(componentId, out var component) || component is not ApplicationPool pool)
                {
                    continue;
                }

                candidates.Add(new CorrelationCandidate
                {
                    RuleId = Id,
                    SourceEntityId = application.Id,
                    TargetEntityId = pool.Id,
                    Type = DependencyEdgeType.Uses,
                    Confidence = Confidence.VeryHigh(),
                    Evidence =
                    [
                        new EvidenceRecord
                        {
                            Type = EvidenceType.IisConfiguration,
                            Location = application.Id,
                            Detail = $"Application.ComponentEntityIds references ApplicationPool {pool.Id}"
                        }
                    ]
                });
            }
        }

        return candidates;
    }
}
