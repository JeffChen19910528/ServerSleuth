using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Risk.Rules;

/// <summary>
/// RR2 (skill.md (Phase 7A) §13): a discovered <see cref="Dll"/> entity whose file was
/// confirmed absent on disk (<c>Metadata["FileStatus"] == "NotFound"</c>, set by both the
/// Windows PE scanner and the Linux native-dependency scanner) — but only when something else
/// in the graph actually depends on it. A missing-and-unreferenced binary is not a migration
/// risk (nothing needs it); an orphan missing binary is never flagged. Severity is derived from
/// the kind of relationship that depends on it: Critical for a Service/ScheduledTask's own
/// executable, High for an application dependency (Imports) or a COM server reference.
/// </summary>
public sealed class MissingBinaryRule : IRiskRule
{
    public string Id => "RR2-MissingBinary";
    public RiskCategory Category => RiskCategory.MissingBinary;
    public RiskSeverity DefaultSeverity => RiskSeverity.High;

    public IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context)
    {
        var findings = new List<RiskFinding>();

        foreach (var dll in context.Dlls)
        {
            if (dll.Metadata.GetValueOrDefault("FileStatus") != "NotFound")
            {
                continue;
            }

            var dependents = context.Graph.EdgesTo(dll.Id).ToList();
            if (dependents.Count == 0)
            {
                continue; // no dependents — nothing on the migration path actually needs this file
            }

            foreach (var edge in dependents)
            {
                if (!context.ById.TryGetValue(edge.SourceEntityId, out var dependent))
                {
                    continue;
                }

                var severity = dependent switch
                {
                    Service or ScheduledTask => RiskSeverity.Critical,
                    _ => RiskSeverity.High
                };

                var confidence = edge.Confidence.Value < dll.Confidence.Value ? edge.Confidence : dll.Confidence;

                findings.Add(new RiskFinding
                {
                    Id = RiskFinding.ComputeId(Id, dll.Id, [edge.SourceEntityId]),
                    RuleId = Id,
                    Category = Category,
                    Severity = severity,
                    Confidence = confidence,
                    Title = $"Missing binary file: {dll.Name}",
                    Description = $"'{dependent.Name}' ({edge.Type}) depends on '{dll.Name}', but that file was not found on disk during discovery.",
                    SourceEntityId = dll.Id,
                    RelatedEntityIds = [edge.SourceEntityId],
                    Evidence = [new EvidenceRecord { Type = EvidenceType.FileSystem, Location = dll.Path ?? dll.Id, Detail = "FileStatus=NotFound" }],
                    Recommendation = "Locate and migrate the missing binary, or confirm the dependent workload no longer requires it before migration.",
                    Metadata = new Dictionary<string, string> { ["MissingBinaryEntityId"] = dll.Id }
                });
            }
        }

        return findings;
    }
}
