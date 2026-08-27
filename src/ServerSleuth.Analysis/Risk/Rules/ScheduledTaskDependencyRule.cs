using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Risk.Rules;

/// <summary>
/// RR7 (skill.md (Phase 7A) §18): a scheduled task whose executable dependency is missing or
/// never resolved. Phrased strictly as a migration risk, never as "this task currently fails."
/// Mirrors <see cref="ServiceDependencyRule"/> exactly, with the (lower, per skill.md's own
/// severity table) High severity a scheduled job warrants versus a service's Critical.
/// </summary>
public sealed class ScheduledTaskDependencyRule : IRiskRule
{
    public string Id => "RR7-ScheduledTaskDependency";
    public RiskCategory Category => RiskCategory.ScheduledTask;
    public RiskSeverity DefaultSeverity => RiskSeverity.High;

    public IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context)
    {
        var findings = new List<RiskFinding>();

        foreach (var task in context.ScheduledTasks)
        {
            if (task.Action is null || !IsExplicitAbsolutePath(task.Action))
            {
                continue; // no explicit executable path to evaluate — never guessed at
            }

            var runsEdges = context.Graph.EdgesFrom(task.Id).Where(e => e.Type == DependencyEdgeType.Runs).ToList();

            if (runsEdges.Count == 0)
            {
                findings.Add(new RiskFinding
                {
                    Id = RiskFinding.ComputeId(Id, task.Id),
                    RuleId = Id,
                    Category = Category,
                    Severity = DefaultSeverity,
                    Confidence = task.Confidence,
                    Title = $"Scheduled task executable dependency unresolved: {task.Name}",
                    Description = $"Scheduled task '{task.Name}' references action '{task.Action}', which could not be resolved to any discovered binary entity.",
                    SourceEntityId = task.Id,
                    Evidence = [new EvidenceRecord { Type = EvidenceType.ScheduledTask, Location = task.Id, Detail = $"Action={task.Action}" }],
                    Recommendation = "Confirm the scheduled task's executable will be present (or rebuilt) on the target environment before migration."
                });
                continue;
            }

            foreach (var edge in runsEdges)
            {
                if (!context.ById.TryGetValue(edge.TargetEntityId, out var target) || target.Metadata.GetValueOrDefault("FileStatus") != "NotFound")
                {
                    continue;
                }

                var confidence = edge.Confidence.Value < task.Confidence.Value ? edge.Confidence : task.Confidence;

                findings.Add(new RiskFinding
                {
                    Id = RiskFinding.ComputeId(Id, task.Id, [target.Id]),
                    RuleId = Id,
                    Category = Category,
                    Severity = DefaultSeverity,
                    Confidence = confidence,
                    Title = $"Scheduled task executable missing on disk: {task.Name}",
                    Description = $"Scheduled task '{task.Name}' runs '{target.Name}', but that file was not found on disk during discovery.",
                    SourceEntityId = task.Id,
                    RelatedEntityIds = [target.Id],
                    Evidence = [new EvidenceRecord { Type = EvidenceType.ScheduledTask, Location = task.Id, Detail = $"Runs {target.Id}" }],
                    Recommendation = "Locate and migrate the missing executable, or rebuild it for the target environment before cutover.",
                    Metadata = new Dictionary<string, string> { ["MissingBinaryEntityId"] = target.Id }
                });
            }
        }

        return findings;
    }

    /// <summary>
    /// Deliberately NOT `System.IO.Path.IsPathRooted` — that BCL method interprets separators
    /// according to the *current* runtime OS (the same class of bug Phase 6G found and fixed
    /// in `ServerSleuth.Analysis`'s correlation rules), so it would wrongly reject a
    /// Windows-style path when Risk Analysis happens to run on Linux. This checks explicitly for
    /// a Linux-absolute path (leading '/'), a Windows drive-letter path ("C:\" or "C:/"), or a
    /// Windows UNC path ("\\server\share"), independent of which OS is executing this code.
    /// </summary>
    private static bool IsExplicitAbsolutePath(string action) =>
        action.StartsWith('/') ||
        action.StartsWith(@"\\", StringComparison.Ordinal) ||
        (action.Length >= 3 && char.IsAsciiLetter(action[0]) && action[1] == ':' && (action[2] == '\\' || action[2] == '/'));
}
