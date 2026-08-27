using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Orchestration;

namespace ServerSleuth.Cli.Tests.Fakes;

/// <summary>
/// Deterministically reproduces mid-discovery cancellation without depending on real-time
/// keyboard input or a race (Phase 10B §9): cancels the SAME <see cref="CancellationTokenSource"/>
/// the test itself is holding, from inside <see cref="RunAsync"/> — i.e. cancellation is
/// requested WHILE discovery is "in progress" (as far as the pipeline can tell), not before the
/// scan even starts. <see cref="RunAsync"/> then throws via
/// <see cref="CancellationToken.ThrowIfCancellationRequested"/>, exactly like a real scanner
/// loop checking its token would.
/// </summary>
internal sealed class CancellingFakeDiscoveryEngine(CancellationTokenSource cancellationTokenSource) : IDiscoveryEngine
{
    public Task<AggregateDiscoveryResult> RunAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        cancellationTokenSource.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("Unreachable — ThrowIfCancellationRequested should have thrown.");
    }
}
