using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Correlation.Rules;

/// <summary>
/// Rule 5 (skill.md §14): Windows Service --RUNS--> Binary. Service.ExecutablePath is the raw
/// registry ImagePath value, which may be quoted and/or carry trailing arguments (e.g.
/// "C:\Foo\bar.exe" -k netsvcs) — CommandLineReference performs the same never-guess quoted/
/// ambiguous-path parsing used for COM server references (Phase 4B) before the result is
/// normalized and resolved against discovered binaries.
/// </summary>
public sealed class ServiceRunsBinaryRule : ICorrelationRule
{
    public string Id => "R5-ServiceRunsBinary";

    public IReadOnlyList<CorrelationCandidate> Evaluate(CorrelationContext context)
    {
        var candidates = new List<CorrelationCandidate>();

        foreach (var service in context.Services)
        {
            if (service.ExecutablePath is null)
            {
                continue;
            }

            var parsed = CommandLineReference.Parse(service.ExecutablePath);
            if (parsed.ExecutablePath is null)
            {
                candidates.Add(new CorrelationCandidate
                {
                    RuleId = Id,
                    SourceEntityId = service.Id,
                    Type = DependencyEdgeType.Runs,
                    Confidence = Confidence.VeryHigh(),
                    UnresolvedReason = $"Service ImagePath '{service.ExecutablePath}' could not be unambiguously parsed into an executable path"
                });
                continue;
            }

            var target = context.TryResolveDllByPath(parsed.ExecutablePath);
            if (target is null)
            {
                candidates.Add(new CorrelationCandidate
                {
                    RuleId = Id,
                    SourceEntityId = service.Id,
                    Type = DependencyEdgeType.Runs,
                    Confidence = Confidence.VeryHigh(),
                    UnresolvedReason = $"Executable '{parsed.ExecutablePath}' was not among discovered binaries"
                });
                continue;
            }

            candidates.Add(new CorrelationCandidate
            {
                RuleId = Id,
                SourceEntityId = service.Id,
                TargetEntityId = target.Id,
                Type = DependencyEdgeType.Runs,
                Confidence = Confidence.VeryHigh(),
                Evidence =
                [
                    new EvidenceRecord
                    {
                        Type = EvidenceType.Registry,
                        Location = $@"HKLM\SYSTEM\CurrentControlSet\Services\{service.Name}",
                        Detail = $"ImagePath resolves to {parsed.ExecutablePath}"
                    }
                ]
            });
        }

        return candidates;
    }
}
