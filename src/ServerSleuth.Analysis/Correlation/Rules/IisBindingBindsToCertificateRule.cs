using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Correlation.Rules;

/// <summary>Rule 8 (skill.md §14): IIS Binding --BINDS_TO--> Certificate. Matches on normalized
/// thumbprint (the same uppercase/whitespace-stripped form WindowsCertificateScanner already
/// produces). If a thumbprint resolves to more than one Certificate entity (the same
/// certificate installed into multiple stores), an edge is created to each — this is a genuine
/// multi-target relationship, not a guess.</summary>
public sealed class IisBindingBindsToCertificateRule : ICorrelationRule
{
    public string Id => "R8-IisBindingBindsToCertificate";

    public IReadOnlyList<CorrelationCandidate> Evaluate(CorrelationContext context)
    {
        var candidates = new List<CorrelationCandidate>();

        foreach (var site in context.WebSites)
        {
            var index = 0;
            while (site.Metadata.TryGetValue($"Binding{index}.CertificateThumbprint", out var rawThumbprint))
            {
                var normalized = CorrelationContext.NormalizeThumbprint(rawThumbprint);

                if (context.CertificatesByNormalizedThumbprint.TryGetValue(normalized, out var certificates))
                {
                    foreach (var certificate in certificates)
                    {
                        candidates.Add(new CorrelationCandidate
                        {
                            RuleId = Id,
                            SourceEntityId = site.Id,
                            TargetEntityId = certificate.Id,
                            Type = DependencyEdgeType.Binds,
                            Confidence = Confidence.VeryHigh(),
                            Evidence =
                            [
                                new EvidenceRecord
                                {
                                    Type = EvidenceType.IisConfiguration,
                                    Location = $"{site.Id} Binding{index}",
                                    Detail = $"CertificateThumbprint={normalized}"
                                }
                            ]
                        });
                    }
                }
                else
                {
                    candidates.Add(new CorrelationCandidate
                    {
                        RuleId = Id,
                        SourceEntityId = site.Id,
                        Type = DependencyEdgeType.Binds,
                        Confidence = Confidence.VeryHigh(),
                        UnresolvedReason = $"Binding{index} thumbprint '{normalized}' did not match any discovered certificate"
                    });
                }

                index++;
            }
        }

        return candidates;
    }
}
