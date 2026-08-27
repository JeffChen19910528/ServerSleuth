using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Risk.Models;

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
}
