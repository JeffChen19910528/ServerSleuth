using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Correlation.Rules;

/// <summary>
/// Rule 10 (skill.md §14), Runtime slice only: Configuration --REFERENCES--> Runtime.
/// Confidence is Low: a runtime marker found in configuration text (e.g. "runtimeconfig.json",
/// "JAVA_HOME") is a textual hint, not an explicit reference to a specific installed runtime,
/// per skill.md §10 ("Configuration mentions '.NET': Low/Medium depending on evidence").
///
/// Database/Endpoint/UNC references from Configuration.DetectedDependencyReferences are
/// deliberately NOT correlated here: no scanner yet produces Database/ExternalDependency/UNC
/// share entities, and creating an edge to a node that doesn't exist would violate skill.md
/// §12/§17 ("no evidence = no edge", "invalid entity references must be rejected"). They
/// remain visible as Configuration entity metadata only until a future phase adds those
/// entity types.
/// </summary>
public sealed class ConfigurationReferencesRuntimeRule : ICorrelationRule
{
    private const string RuntimePrefix = "Runtime: ";

    public string Id => "R10-ConfigurationReferencesRuntime";

    public IReadOnlyList<CorrelationCandidate> Evaluate(CorrelationContext context)
    {
        var candidates = new List<CorrelationCandidate>();

        foreach (var configuration in context.Configurations)
        {
            foreach (var reference in configuration.DetectedDependencyReferences)
            {
                if (!reference.StartsWith(RuntimePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var family = reference[RuntimePrefix.Length..];
                var matches = context.Runtimes.Where(runtime => MatchesFamily(runtime, family)).ToList();

                if (matches.Count == 0)
                {
                    candidates.Add(new CorrelationCandidate
                    {
                        RuleId = Id,
                        SourceEntityId = configuration.Id,
                        Type = DependencyEdgeType.References,
                        Confidence = Confidence.Low(),
                        UnresolvedReason = $"Runtime marker '{family}' did not match any discovered runtime"
                    });
                    continue;
                }

                foreach (var runtime in matches)
                {
                    candidates.Add(new CorrelationCandidate
                    {
                        RuleId = Id,
                        SourceEntityId = configuration.Id,
                        TargetEntityId = runtime.Id,
                        Type = DependencyEdgeType.References,
                        Confidence = Confidence.Low(),
                        Evidence =
                        [
                            new EvidenceRecord
                            {
                                Type = EvidenceType.ConfigurationFile,
                                Location = configuration.Path ?? configuration.Id,
                                Detail = $"Runtime marker: {family}"
                            }
                        ]
                    });
                }
            }
        }

        return candidates;
    }

    private static bool MatchesFamily(Runtime runtime, string configFamily)
    {
        var actualFamily = runtime.Metadata.GetValueOrDefault("Family") ?? runtime.Type;
        return actualFamily == configFamily || actualFamily.StartsWith(configFamily, StringComparison.Ordinal);
    }
}
