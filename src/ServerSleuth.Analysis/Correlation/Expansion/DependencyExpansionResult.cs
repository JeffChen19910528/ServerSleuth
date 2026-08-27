using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Correlation.Expansion;

public sealed record DependencyExpansionResult
{
    public required IReadOnlyList<ExternalDependency> ExternalDependencies { get; init; }

    /// <summary>A new graph containing every node/edge from the Phase 5A graph handed in,
    /// plus the new ExternalDependency nodes and Configuration→REFERENCES→(ExternalDependency
    /// | Runtime) edges this phase adds. The original graph passed to <see cref="DependencyExpansionEngine"/>
    /// is never mutated.</summary>
    public required DependencyGraph ExpandedGraph { get; init; }

    public required IReadOnlyList<DerivedWorkloadDependency> DerivedWorkloadDependencies { get; init; }
    public required ExpansionDiagnostics Diagnostics { get; init; }
}
