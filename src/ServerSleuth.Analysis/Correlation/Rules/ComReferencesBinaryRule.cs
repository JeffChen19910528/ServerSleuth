using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Correlation.Rules;

/// <summary>Rule 7 (skill.md §14): COM Registration --REFERENCES--> Binary. InprocServer32/
/// LocalServer32 are already clean, argument-free paths (WindowsComScanner's ServerReference
/// parsing happened at scan time), so only path normalization is needed here.</summary>
public sealed class ComReferencesBinaryRule : ICorrelationRule
{
    public string Id => "R7-ComReferencesBinary";

    public IReadOnlyList<CorrelationCandidate> Evaluate(CorrelationContext context)
    {
        var candidates = new List<CorrelationCandidate>();

        foreach (var component in context.ComComponents)
        {
            AddCandidate(context, component, component.InprocServer32, "InprocServer32", candidates);
            AddCandidate(context, component, component.LocalServer32, "LocalServer32", candidates);
        }

        return candidates;
    }

    private void AddCandidate(
        CorrelationContext context,
        ComComponent component,
        string? serverPath,
        string valueName,
        List<CorrelationCandidate> candidates)
    {
        if (serverPath is null)
        {
            return;
        }

        var target = context.TryResolveDllByPath(serverPath);
        if (target is null)
        {
            candidates.Add(new CorrelationCandidate
            {
                RuleId = Id,
                SourceEntityId = component.Id,
                Type = DependencyEdgeType.References,
                Confidence = Confidence.VeryHigh(),
                UnresolvedReason = $"{valueName} path '{serverPath}' was not among discovered binaries"
            });
            return;
        }

        candidates.Add(new CorrelationCandidate
        {
            RuleId = Id,
            SourceEntityId = component.Id,
            TargetEntityId = target.Id,
            Type = DependencyEdgeType.References,
            Confidence = Confidence.VeryHigh(),
            Evidence =
            [
                new EvidenceRecord
                {
                    Type = EvidenceType.Registry,
                    Location = $@"HKLM\Software\Classes\CLSID\{component.Clsid}\{valueName}",
                    Detail = $"{valueName} resolves to {serverPath}"
                }
            ]
        });
    }
}
