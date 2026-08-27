using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Correlation.Rules;

/// <summary>
/// Rule 9 (skill.md §14): Binary --IMPORTS--> Binary. Resolution is deliberately restricted to
/// the importing binary's own directory: an import named "Vendor.dll" only ever resolves to a
/// copy discovered in the same folder as the importer, never to a same-named DLL discovered
/// under an unrelated application (skill.md §23's negative fixture). No filesystem rescanning
/// is performed — only the already-discovered Dll index is consulted (skill.md §24).
/// </summary>
public sealed class BinaryImportsBinaryRule : ICorrelationRule
{
    public string Id => "R9-BinaryImportsBinary";

    public IReadOnlyList<CorrelationCandidate> Evaluate(CorrelationContext context)
    {
        var candidates = new List<CorrelationCandidate>();

        foreach (var dll in context.Dlls)
        {
            if (dll.Path is null)
            {
                continue;
            }

            var importsRaw = dll.Metadata.GetValueOrDefault("Imports");
            if (string.IsNullOrEmpty(importsRaw))
            {
                continue;
            }

            var directory = WindowsPathNormalizer.GetDirectoryName(dll.Path);
            if (directory is null)
            {
                continue;
            }

            foreach (var importName in importsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var candidatePath = WindowsPathNormalizer.Combine(directory, importName);
                var target = context.TryResolveDllByPath(candidatePath);

                if (target is null)
                {
                    candidates.Add(new CorrelationCandidate
                    {
                        RuleId = Id,
                        SourceEntityId = dll.Id,
                        Type = DependencyEdgeType.Imports,
                        Confidence = Confidence.High(),
                        UnresolvedReason = $"Import '{importName}' was not resolved to a discovered binary in {directory}"
                    });
                    continue;
                }

                candidates.Add(new CorrelationCandidate
                {
                    RuleId = Id,
                    SourceEntityId = dll.Id,
                    TargetEntityId = target.Id,
                    Type = DependencyEdgeType.Imports,
                    Confidence = Confidence.High(),
                    Evidence =
                    [
                        new EvidenceRecord
                        {
                            Type = EvidenceType.PeMetadata,
                            Location = dll.Path,
                            Detail = $"Import table references {importName}"
                        }
                    ]
                });
            }
        }

        return candidates;
    }
}
