using ServerSleuth.Core.Interfaces;

namespace ServerSleuth.Core.Orchestration;

/// <summary>Default <see cref="IDiscoveryScannerRegistry"/> — takes whatever scanners the
/// composition root's DI container resolved for <see cref="IDiscoveryScanner"/> (via
/// `AddServerSleuthWindows()`/`AddServerSleuthLinux()`/both) and sorts them once, deterministically,
/// by <see cref="IDiscoveryScanner.Id"/>. See skill.md (Phase 6G) §3, §9.</summary>
public sealed class DiscoveryScannerRegistry : IDiscoveryScannerRegistry
{
    public DiscoveryScannerRegistry(IEnumerable<IDiscoveryScanner> scanners)
    {
        Scanners = scanners.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<IDiscoveryScanner> Scanners { get; }
}
