using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Infrastructure.DependencyInjection;

namespace ServerSleuth.Infrastructure.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddServerSleuthDiscoveryEngine_RegistersRegistryAndEngine()
    {
        var provider = new ServiceCollection().AddServerSleuthDiscoveryEngine().BuildServiceProvider();

        Assert.IsType<DiscoveryScannerRegistry>(provider.GetRequiredService<IDiscoveryScannerRegistry>());
        Assert.IsType<DiscoveryEngine>(provider.GetRequiredService<IDiscoveryEngine>());
    }

    [Fact]
    public void AddServerSleuthDiscoveryEngine_WithNoScannersRegistered_ProducesAnEmptyRegistry_NeverThrows()
    {
        var provider = new ServiceCollection().AddServerSleuthDiscoveryEngine().BuildServiceProvider();

        var registry = provider.GetRequiredService<IDiscoveryScannerRegistry>();

        Assert.Empty(registry.Scanners);
    }
}
