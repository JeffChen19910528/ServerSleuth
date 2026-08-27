using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Orchestration;

namespace ServerSleuth.Gui.ExecutionHost.Tests.Fakes;

/// <summary>Returns a pre-built <see cref="AggregateDiscoveryResult"/> instead of running any
/// real scanner — mirrors <c>ServerSleuth.Cli.Tests.Fakes.FakeDiscoveryEngine</c>'s own
/// reasoning exactly (skill.md's "use fakes for unit tests, never a real machine's own
/// scanners").</summary>
internal sealed class FakeDiscoveryEngine(AggregateDiscoveryResult result, TimeSpan? delay = null) : IDiscoveryEngine
{
    public async Task<AggregateDiscoveryResult> RunAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        if (delay is { } d)
        {
            await Task.Delay(d, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
