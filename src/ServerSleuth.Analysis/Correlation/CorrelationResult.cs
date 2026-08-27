using ServerSleuth.Analysis.Correlation.Diagnostics;
using ServerSleuth.Core.Graph;

namespace ServerSleuth.Analysis.Correlation;

public sealed record CorrelationResult
{
    public required DependencyGraph Graph { get; init; }
    public required CorrelationDiagnostics Diagnostics { get; init; }
}
