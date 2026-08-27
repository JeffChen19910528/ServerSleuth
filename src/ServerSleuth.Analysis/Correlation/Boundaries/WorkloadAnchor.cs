using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Correlation.Boundaries;

/// <summary>A single strong-identity seed for a workload boundary — one per IIS Application,
/// Windows Service (with a resolvable executable), or Scheduled Task (with a resolvable
/// executable). See skill.md §4-6.</summary>
public sealed record WorkloadAnchor
{
    public required string AnchorEntityId { get; init; }
    public required WorkloadAnchorKind Kind { get; init; }
    public required string Name { get; init; }

    /// <summary>Bounded root directory for this anchor (IIS Application's PhysicalPath, or the
    /// directory containing the Service/Task's resolved executable) — used only for the
    /// diagnostic-only "common parent" candidate check (skill.md §9), never for merging.</summary>
    public string? RootPath { get; init; }

    public required EvidenceRecord SelfEvidence { get; init; }
}
