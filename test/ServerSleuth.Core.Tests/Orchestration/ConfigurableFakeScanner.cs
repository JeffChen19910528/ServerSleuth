using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Core.Tests.Orchestration;

/// <summary>A configurable IDiscoveryScanner double for exercising DiscoveryEngine/
/// DiscoveryScannerRegistry — deliberately more flexible than the fixed-shape FakeScanner used
/// by Phase 1's own interface-contract tests.</summary>
internal sealed class ConfigurableFakeScanner(
    string id,
    ScannerStatus status = ScannerStatus.Supported,
    IReadOnlyList<DiscoveryEntity>? entities = null,
    IReadOnlyList<DiscoveryError>? errors = null,
    Exception? throwOnScan = null,
    PlatformSupport platformSupport = PlatformSupport.Both) : IDiscoveryScanner
{
    public int InvocationCount { get; private set; }

    public string Id => id;
    public PlatformSupport PlatformSupport => platformSupport;

    public Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        InvocationCount++;

        if (throwOnScan is not null)
        {
            throw throwOnScan;
        }

        return Task.FromResult(new DiscoveryResult
        {
            ScannerId = id,
            Status = status,
            Entities = entities ?? [],
            Errors = errors ?? []
        });
    }

    public static Software MakeEntity(string id, string name = "Fake") => new()
    {
        Id = id,
        Name = name,
        Type = "Software",
        Source = "Fake",
        Status = EntityStatus.Installed
    };
}
