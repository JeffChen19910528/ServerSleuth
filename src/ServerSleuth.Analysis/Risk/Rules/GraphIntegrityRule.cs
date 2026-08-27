using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Risk.Rules;

/// <summary>
/// RR12 (skill.md (Phase 7A) §23): converts only Error-severity <see cref="ValidationFinding"/>
/// records (dangling edges, missing evidence, confidence without evidence, and similar hard
/// structural problems) into RiskFindings — Warning/Info-level validator findings are never
/// duplicated as migration risks, since GraphValidator itself already deliberately reserves
/// Error for problems that actually warrant attention (skill.md (Phase 5D) §2-10). The original
/// validation finding's Category/Code is preserved as metadata for provenance, never discarded.
/// </summary>
public sealed class GraphIntegrityRule : IRiskRule
{
    public string Id => "RR12-GraphIntegrity";
    public RiskCategory Category => RiskCategory.GraphIntegrity;
    public RiskSeverity DefaultSeverity => RiskSeverity.High;

    public IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context)
    {
        var findings = new List<RiskFinding>();

        foreach (var validationFinding in context.Validation.Findings)
        {
            if (validationFinding.Severity != ValidationSeverity.Error)
            {
                continue;
            }

            var sourceEntityId = validationFinding.EntityIds.FirstOrDefault() ?? validationFinding.Code;

            findings.Add(new RiskFinding
            {
                Id = RiskFinding.ComputeId(Id, sourceEntityId, validationFinding.EntityIds.Skip(1)),
                RuleId = Id,
                Category = Category,
                Severity = DefaultSeverity,
                Confidence = Confidence.VeryHigh(),
                Title = $"Graph integrity error: {validationFinding.Code}",
                Description = validationFinding.Message,
                SourceEntityId = sourceEntityId,
                RelatedEntityIds = validationFinding.EntityIds.Skip(1).ToList(),
                Evidence = [new EvidenceRecord { Type = EvidenceType.FileSystem, Location = sourceEntityId, Detail = $"{validationFinding.Category}/{validationFinding.Code}" }],
                Recommendation = "Investigate this structural inconsistency before relying on the dependency graph for migration planning.",
                Metadata = new Dictionary<string, string>
                {
                    ["ValidationCategory"] = validationFinding.Category,
                    ["ValidationCode"] = validationFinding.Code
                }
            });
        }

        return findings;
    }
}
