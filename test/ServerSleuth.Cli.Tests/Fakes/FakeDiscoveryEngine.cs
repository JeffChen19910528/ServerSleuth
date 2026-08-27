using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Orchestration;

namespace ServerSleuth.Cli.Tests.Fakes;

/// <summary>Returns a pre-built <see cref="AggregateDiscoveryResult"/> instead of running any
/// real scanner — see skill.md (Phase 10A) §22: CLI unit tests use fakes, never a real Windows/
/// Linux machine's own scanners.</summary>
internal sealed class FakeDiscoveryEngine(AggregateDiscoveryResult result) : IDiscoveryEngine
{
    public Task<AggregateDiscoveryResult> RunAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(result);
    }
}
