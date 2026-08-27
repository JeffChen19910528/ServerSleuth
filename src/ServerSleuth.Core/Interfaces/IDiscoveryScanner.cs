using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Core.Interfaces;

/// <summary>
/// Contract every scanner implements. A scanner's only job is finding raw facts and attaching
/// evidence — never correlating across scanners, never scoring risk. See skill.md §38.
/// </summary>
public interface IDiscoveryScanner
{
    string Id { get; }
    PlatformSupport PlatformSupport { get; }

    Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken);
}
