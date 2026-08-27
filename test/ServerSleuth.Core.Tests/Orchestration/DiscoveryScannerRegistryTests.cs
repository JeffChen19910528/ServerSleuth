using ServerSleuth.Core.Orchestration;

namespace ServerSleuth.Core.Tests.Orchestration;

public class DiscoveryScannerRegistryTests
{
    [Fact]
    public void Scanners_AreOrderedDeterministicallyById_RegardlessOfConstructionOrder()
    {
        var scanners = new[]
        {
            new ConfigurableFakeScanner("zebra-scanner"),
            new ConfigurableFakeScanner("alpha-scanner"),
            new ConfigurableFakeScanner("mike-scanner")
        };

        var registry = new DiscoveryScannerRegistry(scanners);

        Assert.Equal(["alpha-scanner", "mike-scanner", "zebra-scanner"], registry.Scanners.Select(s => s.Id));
    }

    [Fact]
    public void Scanners_OrderingIsStableAcrossRepeatedConstruction_NeverDictionaryOrder()
    {
        var scanners = new[]
        {
            new ConfigurableFakeScanner("c-scanner"),
            new ConfigurableFakeScanner("a-scanner"),
            new ConfigurableFakeScanner("b-scanner")
        };

        var registryA = new DiscoveryScannerRegistry(scanners);
        var registryB = new DiscoveryScannerRegistry(scanners);

        Assert.Equal(registryA.Scanners.Select(s => s.Id), registryB.Scanners.Select(s => s.Id));
    }

    [Fact]
    public void Scanners_EmptyRegistration_ProducesEmptyRegistry_NeverThrows()
    {
        var registry = new DiscoveryScannerRegistry([]);

        Assert.Empty(registry.Scanners);
    }
}
