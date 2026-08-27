using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Core.Tests.Interfaces;

/// <summary>A minimal IDiscoveryScanner used only to exercise the contract in tests —
/// no real scanner implementation belongs in Core.</summary>
internal sealed class FakeScanner : IDiscoveryScanner
{
    public string Id => "fake-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Both;

    public Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Software entity = new()
        {
            Id = "fake-1",
            Name = "Fake Software",
            Type = "Software",
            Source = "Fake",
            Status = EntityStatus.Installed
        };

        return Task.FromResult(DiscoveryResult.Success(Id, [entity]));
    }
}
