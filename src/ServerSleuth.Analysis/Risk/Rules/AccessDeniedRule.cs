using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Risk.Rules;

/// <summary>
/// RR3 (skill.md (Phase 7A) §14): an entity ServerSleuth could not fully inspect
/// (<c>Metadata["ParseStatus"] == "AccessDenied"</c> on a Configuration, or
/// <c>Metadata["FileStatus"] == "AccessDenied"</c> on a Dll). Critically, AccessDenied means
/// "ServerSleuth could not verify this area," never "this component is broken" — every
/// Title/Description here is phrased as an uncertainty, not a defect claim. Confidence is
/// VeryHigh that access was in fact denied (that much is a hard scanner fact); it says nothing
/// about the underlying component's actual health.
/// </summary>
public sealed class AccessDeniedRule : IRiskRule
{
    public string Id => "RR3-AccessDenied";
    public RiskCategory Category => RiskCategory.AccessDenied;
    public RiskSeverity DefaultSeverity => RiskSeverity.Medium;

    public IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context)
    {
        var findings = new List<RiskFinding>();

        foreach (var configuration in context.Configurations)
        {
            if (configuration.Metadata.GetValueOrDefault("ParseStatus") != "AccessDenied")
            {
                continue;
            }

            findings.Add(Build(configuration, "Configuration", "the configuration scan"));
        }

        foreach (var dll in context.Dlls)
        {
            if (dll.Metadata.GetValueOrDefault("FileStatus") != "AccessDenied")
            {
                continue;
            }

            findings.Add(Build(dll, "Binary", "the binary discovery scan"));
        }

        return findings;

        RiskFinding Build(DiscoveryEntity entity, string kind, string scanDescription)
        {
            var isBoundaryMember = context.BoundaryIdByEntityId.TryGetValue(entity.Id, out var boundaryId);

            return new RiskFinding
            {
                Id = RiskFinding.ComputeId(Id, entity.Id),
                RuleId = Id,
                Category = Category,
                Severity = isBoundaryMember ? RiskSeverity.High : RiskSeverity.Medium,
                Confidence = Confidence.VeryHigh(),
                Title = $"{kind} could not be fully inspected",
                Description = $"{scanDescription} was partially supported because access to '{entity.Name}' was denied. Migration completeness cannot be confirmed from current evidence — this does not mean the underlying {kind.ToLowerInvariant()} is broken, only that it could not be verified.",
                SourceEntityId = entity.Id,
                ApplicationBoundaryId = boundaryId,
                Evidence = [new EvidenceRecord { Type = EvidenceType.FileSystem, Location = entity.Path ?? entity.Id, Detail = "AccessDenied" }],
                Recommendation = "Re-run discovery with elevated permissions to confirm this component's actual state before finalizing the migration plan."
            };
        }
    }
}
