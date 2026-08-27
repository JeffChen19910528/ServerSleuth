using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Correlation.Rules;

/// <summary>Rule 6 (skill.md §14): Scheduled Task --RUNS--> Binary. ScheduledTask.Action is
/// already the Execute action's bare Path (WindowsScheduledTaskScanner reads it directly from
/// the Task Scheduler COM API, not a shell command line) — CommandLineReference parsing is
/// still applied defensively in case a task's action path was authored with quotes.</summary>
public sealed class ScheduledTaskRunsBinaryRule : ICorrelationRule
{
    public string Id => "R6-ScheduledTaskRunsBinary";

    public IReadOnlyList<CorrelationCandidate> Evaluate(CorrelationContext context)
    {
        var candidates = new List<CorrelationCandidate>();

        foreach (var task in context.ScheduledTasks)
        {
            if (task.Action is null)
            {
                continue;
            }

            var parsed = CommandLineReference.Parse(task.Action);
            if (parsed.ExecutablePath is null)
            {
                candidates.Add(new CorrelationCandidate
                {
                    RuleId = Id,
                    SourceEntityId = task.Id,
                    Type = DependencyEdgeType.Runs,
                    Confidence = Confidence.VeryHigh(),
                    UnresolvedReason = $"Scheduled Task action '{task.Action}' could not be unambiguously parsed into an executable path"
                });
                continue;
            }

            var target = context.TryResolveDllByPath(parsed.ExecutablePath);
            if (target is null)
            {
                candidates.Add(new CorrelationCandidate
                {
                    RuleId = Id,
                    SourceEntityId = task.Id,
                    Type = DependencyEdgeType.Runs,
                    Confidence = Confidence.VeryHigh(),
                    UnresolvedReason = $"Executable '{parsed.ExecutablePath}' was not among discovered binaries"
                });
                continue;
            }

            candidates.Add(new CorrelationCandidate
            {
                RuleId = Id,
                SourceEntityId = task.Id,
                TargetEntityId = target.Id,
                Type = DependencyEdgeType.Runs,
                Confidence = Confidence.VeryHigh(),
                Evidence =
                [
                    new EvidenceRecord
                    {
                        Type = EvidenceType.ScheduledTask,
                        Location = task.Id,
                        Detail = $"Execute action resolves to {parsed.ExecutablePath}"
                    }
                ]
            });
        }

        return candidates;
    }
}
