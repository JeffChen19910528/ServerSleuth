using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Orchestration;

namespace ServerSleuth.Analysis.Orchestration;

/// <summary>The pipeline artifacts a completed scan produces, downstream of discovery — see
/// skill.md (Phase 10A) §8: these are treated as opaque pipeline artifacts the caller
/// coordinates, never reinterpreted. Moved here from <c>ServerSleuth.Cli.Pipeline</c> in Phase
/// GUI-3 so both the CLI and the GUI execution host can share the exact same orchestration type
/// (see <see cref="ScanPipelineRunner"/>'s own doc comment) — this project only ever used
/// <c>ServerSleuth.Core</c>/<c>ServerSleuth.Analysis</c> types, so the move changes no
/// dependency direction anywhere.</summary>
public sealed record ScanPipelineResult
{
    public required RiskAggregationResult Aggregation { get; init; }
    public required ServerMigrationAssessmentReport Report { get; init; }

    /// <summary>GUI-6A: the exact same <see cref="AggregateDiscoveryResult"/>
    /// <see cref="ScanPipelineRunner.DiscoverAsync"/> already produced — never re-run, never
    /// re-derived. Carried through purely so a presentation layer (the Results Dashboard's
    /// Discovery Inventory) can show the raw discovered entities that fed Analysis, without the
    /// GUI ever touching <c>IDiscoveryEngine</c> itself. Defaults to an empty result only for
    /// the handful of hand-built fixtures/tests that predate this field and never populate it.</summary>
    public AggregateDiscoveryResult Discovery { get; init; } = EmptyDiscovery;

    /// <summary>GUI-6A: the exact same <see cref="Core.Boundaries.ApplicationBoundary"/> list
    /// Phase 5B's <c>ApplicationBoundaryEngine</c> already computed inside
    /// <see cref="ScanPipelineRunner.Analyze"/> — previously a local variable, discarded once
    /// <see cref="Report"/> was built. No new boundary analysis is performed to populate this;
    /// it is the same <c>BoundaryAnalysisResult.Boundaries</c> instance.</summary>
    public IReadOnlyList<ApplicationBoundary> Boundaries { get; init; } = [];

    /// <summary>GUI-6A: the exact same <see cref="ExternalDependency"/> list Phase 5C's
    /// <c>DependencyExpansionEngine</c> already produced inside <see cref="ScanPipelineRunner.Analyze"/>
    /// — previously discarded once folded into the dependency graph. These are
    /// <see cref="DiscoveryEntity"/>-shaped (evidence, confidence, status) even though they are
    /// an Analysis-layer derivation rather than a raw scanner result, so the Inventory can list
    /// them as their own category alongside <see cref="Discovery"/>'s entities.</summary>
    public IReadOnlyList<ExternalDependency> ExternalDependencies { get; init; } = [];

    private static readonly AggregateDiscoveryResult EmptyDiscovery = new()
    {
        Entities = [],
        Errors = [],
        ScannerResults = [],
        ScannerStatuses = new Dictionary<string, Core.Enums.ScannerStatus>()
    };
}
