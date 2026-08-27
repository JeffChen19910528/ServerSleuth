using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Risk.Rules;

/// <summary>
/// RR1 (skill.md (Phase 7A) §12): a binary's DT_NEEDED/PE-import table names a library that
/// never resolved to any discovered binary entity at all — distinct from
/// <see cref="MissingBinaryRule"/>, which covers a discovered binary whose file is confirmed
/// absent on disk. Reads <see cref="GraphValidationResult"/>'s already-computed
/// "UnresolvedBinary" findings (Phase 5D) rather than re-deriving import resolution — this rule
/// never re-runs correlation, only reinterprets its already-produced output as a migration risk.
/// </summary>
public sealed class MissingDependencyRule : IRiskRule
{
    public string Id => "RR1-MissingDependency";
    public RiskCategory Category => RiskCategory.MissingDependency;
    public RiskSeverity DefaultSeverity => RiskSeverity.High;

    public IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context)
    {
        var findings = new List<RiskFinding>();

        foreach (var validationFinding in context.Validation.Findings)
        {
            if (validationFinding.Code != "UnresolvedBinary" || validationFinding.EntityIds.Count == 0)
            {
                continue;
            }

            var sourceEntityId = validationFinding.EntityIds[0];
            if (!context.ById.TryGetValue(sourceEntityId, out var sourceEntity))
            {
                continue; // defensive — the validator only ever cites real entity IDs, but never guess if it somehow didn't
            }

            findings.Add(new RiskFinding
            {
                Id = RiskFinding.ComputeId(Id, sourceEntityId),
                RuleId = Id,
                Category = Category,
                Severity = DefaultSeverity,
                Confidence = sourceEntity.Confidence,
                Title = "Native/managed dependency did not resolve to any discovered binary",
                Description = validationFinding.Message,
                SourceEntityId = sourceEntityId,
                Evidence = [new EvidenceRecord { Type = EvidenceType.BinaryImport, Location = sourceEntity.Path ?? sourceEntityId, Detail = validationFinding.Message }],
                Recommendation = "Confirm the required library is available (and migrated) on the target environment, or bundle it alongside this binary."
            });
        }

        return findings;
    }
}
