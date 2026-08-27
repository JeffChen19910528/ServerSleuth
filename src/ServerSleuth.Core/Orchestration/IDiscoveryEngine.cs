using ServerSleuth.Core.Interfaces;

namespace ServerSleuth.Core.Orchestration;

/// <summary>Runs every scanner in an <see cref="IDiscoveryScannerRegistry"/> and produces one
/// deterministic <see cref="AggregateDiscoveryResult"/> — see skill.md (Phase 6G) §2.</summary>
public interface IDiscoveryEngine
{
    Task<AggregateDiscoveryResult> RunAsync(DiscoveryContext context, CancellationToken cancellationToken);
}
