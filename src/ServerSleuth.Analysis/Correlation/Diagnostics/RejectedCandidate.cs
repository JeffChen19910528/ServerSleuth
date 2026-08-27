namespace ServerSleuth.Analysis.Correlation.Diagnostics;

/// <summary>A candidate relationship that did not become a graph edge, and why — see skill.md §16.</summary>
public sealed record RejectedCandidate
{
    public required string RuleId { get; init; }
    public required string SourceEntityId { get; init; }
    public string? TargetHint { get; init; }
    public required string Reason { get; init; }
}
