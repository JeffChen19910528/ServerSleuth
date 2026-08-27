using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Risk.Rules;

/// <summary>
/// RR10 (skill.md (Phase 7A) §21): surfaces every shared-execution-target situation Phase 5B's
/// <c>ApplicationBoundaryEngine</c> already identified and deliberately did NOT merge into one
/// boundary (<c>BoundaryDiagnostics.SharedBinaries</c>) — never re-derived here. Framed strictly
/// as "this shared dependency must be preserved for every workload during migration," never as
/// "shared infrastructure is inherently unsafe."
/// </summary>
public sealed class SharedInfrastructureRule : IRiskRule
{
    public string Id => "RR10-SharedInfrastructure";
    public RiskCategory Category => RiskCategory.SharedInfrastructure;
    public RiskSeverity DefaultSeverity => RiskSeverity.Medium;

    public IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context)
    {
        var findings = new List<RiskFinding>();

        foreach (var shared in context.BoundaryDiagnostics.SharedBinaries)
        {
            if (shared.SharingAnchorIds.Count < 2)
            {
                continue; // sharing requires at least two independent workloads by definition
            }

            if (!context.ById.TryGetValue(shared.DllEntityId, out var dll))
            {
                continue;
            }

            findings.Add(new RiskFinding
            {
                Id = RiskFinding.ComputeId(Id, shared.DllEntityId, shared.SharingAnchorIds),
                RuleId = Id,
                Category = Category,
                Severity = DefaultSeverity,
                Confidence = dll.Confidence,
                Title = $"Shared execution target: {dll.Name}",
                Description = $"'{dll.Name}' is shared by {shared.SharingAnchorIds.Count} independent workloads ({shared.Reason}). Migration must preserve this shared dependency for every one of them — it was deliberately not merged into a single application boundary.",
                SourceEntityId = shared.DllEntityId,
                RelatedEntityIds = shared.SharingAnchorIds,
                Evidence = [new EvidenceRecord { Type = EvidenceType.FileSystem, Location = dll.Path ?? dll.Id, Detail = shared.Reason }],
                Recommendation = "Ensure the shared binary is migrated once and remains reachable by every workload that depends on it, rather than being duplicated inconsistently."
            });
        }

        return findings;
    }
}
