using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Correlation.Rules;

/// <summary>
/// Rule 3 (skill.md §14): IIS Application --CONFIGURES--> Configuration. Ownership comes
/// directly from Configuration.Metadata["OwnerEntityId"], which Phase 4E-1's ScanRootCollector
/// already assigned unambiguously (one owner per bounded scan root) — this rule does not
/// re-derive ownership from a path prefix, avoiding the "every config file in a directory is
/// automatically relevant" trap (skill.md §15).
/// </summary>
public sealed class ApplicationConfiguresConfigurationRule : ICorrelationRule
{
    public string Id => "R3-ApplicationConfiguresConfiguration";

    public IReadOnlyList<CorrelationCandidate> Evaluate(CorrelationContext context)
    {
        var candidates = new List<CorrelationCandidate>();

        foreach (var configuration in context.Configurations)
        {
            var ownerId = configuration.Metadata.GetValueOrDefault("OwnerEntityId");
            if (ownerId is null)
            {
                continue;
            }

            if (!context.ById.TryGetValue(ownerId, out var owner) || owner is not Application application)
            {
                continue;
            }

            candidates.Add(new CorrelationCandidate
            {
                RuleId = Id,
                SourceEntityId = application.Id,
                TargetEntityId = configuration.Id,
                Type = DependencyEdgeType.Configures,
                Confidence = Confidence.High(),
                Evidence =
                [
                    new EvidenceRecord
                    {
                        Type = EvidenceType.ConfigurationFile,
                        Location = configuration.Path ?? configuration.Id,
                        Detail = $"ScanRoot ownership assigned this configuration file to Application {application.Id}"
                    }
                ]
            });
        }

        return candidates;
    }
}
