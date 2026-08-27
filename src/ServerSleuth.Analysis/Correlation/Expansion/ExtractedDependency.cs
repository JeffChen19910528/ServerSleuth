using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Correlation.Expansion;

/// <summary>One normalized external-dependency observation pulled from a single Configuration
/// entity's already-detected metadata — never from raw file text (Analysis never reads a
/// file). See skill.md (Phase 5C) §17.</summary>
public sealed record ExtractedDependency
{
    public required ExternalDependency Entity { get; init; }

    /// <summary>Human-readable description of what was matched, e.g. "Server=DB01,1433" — used
    /// as the Configuration→REFERENCES→ExternalDependency edge's evidence detail.</summary>
    public required string ReferenceDetail { get; init; }
}
