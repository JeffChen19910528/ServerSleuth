using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Correlation;

/// <summary>
/// A relationship a rule believes it has found, before validation. Validation (performed by
/// <see cref="CorrelationEngine"/>) checks the target actually resolved, both endpoints exist,
/// it isn't a self-edge, and evidence is non-empty — only then does it become a
/// <see cref="ServerSleuth.Core.Graph.DependencyEdge"/>. See skill.md §3, §12, §17.
/// </summary>
public sealed record CorrelationCandidate
{
    public required string RuleId { get; init; }
    public required string SourceEntityId { get; init; }

    /// <summary>Null means the rule could not resolve a target (e.g. an unresolved PE import,
    /// or a command line too ambiguous to parse) — the candidate is preserved for diagnostics
    /// rather than silently dropped, but never becomes a graph edge.</summary>
    public string? TargetEntityId { get; init; }

    public required DependencyEdgeType Type { get; init; }
    public required Confidence Confidence { get; init; }
    public IReadOnlyList<EvidenceRecord> Evidence { get; init; } = [];

    /// <summary>Why <see cref="TargetEntityId"/> is null, or otherwise why this candidate may
    /// be rejected — surfaced in <see cref="CorrelationDiagnostics"/> either way.</summary>
    public string? UnresolvedReason { get; init; }
}
