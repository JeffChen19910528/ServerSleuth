using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Correlation.Rules;

/// <summary>
/// Rule 4 (skill.md §14): IIS Application --CONTAINS--> Binary. Deliberately CONTAINS, not
/// DEPENDS_ON: bounded-physical-path membership is not evidence of actual use (skill.md §2,
/// §15). Confidence is Medium — weaker than an explicit reference (COM/Service/Task) because
/// "found under this application's root" does not prove the application loads this binary.
/// </summary>
public sealed class ApplicationContainsBinaryRule : ICorrelationRule
{
    public string Id => "R4-ApplicationContainsBinary";

    public IReadOnlyList<CorrelationCandidate> Evaluate(CorrelationContext context)
    {
        var candidates = new List<CorrelationCandidate>();

        foreach (var dll in context.Dlls)
        {
            foreach (var ownerId in dll.ReferencedByEntityIds)
            {
                if (!context.ById.TryGetValue(ownerId, out var owner) || owner is not Application application)
                {
                    continue;
                }

                candidates.Add(new CorrelationCandidate
                {
                    RuleId = Id,
                    SourceEntityId = application.Id,
                    TargetEntityId = dll.Id,
                    Type = DependencyEdgeType.Contains,
                    Confidence = Confidence.Medium(),
                    Evidence =
                    [
                        new EvidenceRecord
                        {
                            Type = EvidenceType.FileSystem,
                            Location = dll.Path ?? dll.Id,
                            Detail = $"Discovered under Application {application.Id}'s bounded physical path"
                        }
                    ]
                });
            }
        }

        return candidates;
    }
}
