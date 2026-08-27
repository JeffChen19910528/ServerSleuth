using ServerSleuth.Analysis.Correlation.Boundaries.Diagnostics;
using ServerSleuth.Core.Boundaries;

namespace ServerSleuth.Analysis.Correlation.Boundaries;

public sealed record BoundaryAnalysisResult
{
    public required IReadOnlyList<ApplicationBoundary> Boundaries { get; init; }
    public required BoundaryDiagnostics Diagnostics { get; init; }
}
