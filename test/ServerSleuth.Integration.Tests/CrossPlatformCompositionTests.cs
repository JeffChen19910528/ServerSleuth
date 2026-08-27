using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Infrastructure.DependencyInjection;
using ServerSleuth.Linux;
using ServerSleuth.Windows.DependencyInjection;

namespace ServerSleuth.Integration.Tests;

/// <summary>
/// Proves the cross-platform composition goal of Phase 6G (skill.md §1-4, §21): a single host
/// can compose Infrastructure + Windows + Linux + the discovery orchestrator without any
/// scanner-selection code branching on the current OS. `DiscoveryScannerRegistry`/
/// `DiscoveryEngine` (both in `ServerSleuth.Core`) never reference `ServerSleuth.Windows` or
/// `ServerSleuth.Linux` at all — only this test project (and, in a real host, whatever
/// composition root chooses to call both registration methods) does.
/// </summary>
public class CrossPlatformCompositionTests
{
    private static IServiceProvider ComposeBoth() =>
        new ServiceCollection()
            .AddLogging()
            .AddServerSleuthInfrastructure()
            .AddServerSleuthWindows()
            .AddServerSleuthLinux()
            .AddServerSleuthDiscoveryEngine()
            .BuildServiceProvider();

    private static int CountScanners(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection().AddLogging().AddServerSleuthInfrastructure();
        register(services);
        return services.BuildServiceProvider().GetServices<IDiscoveryScanner>().Count();
    }

    [Fact]
    public void ComposedRegistry_ContainsTheUnionOfBothPlatforms_ExactCountNoDuplicatesNoOmissions()
    {
        var windowsOnlyCount = CountScanners(s => s.AddServerSleuthWindows());
        var linuxOnlyCount = CountScanners(s => s.AddServerSleuthLinux());

        var provider = ComposeBoth();
        var registry = provider.GetRequiredService<IDiscoveryScannerRegistry>();

        Assert.Equal(windowsOnlyCount + linuxOnlyCount, registry.Scanners.Count);

        var ids = registry.Scanners.Select(s => s.Id).ToList();
        Assert.Equal(ids.Distinct(StringComparer.Ordinal).Count(), ids.Count); // no duplicate registrations
    }

    [Fact]
    public void ComposedRegistry_EveryScannerReportsItsOwnDeclaredPlatformSupport_NeverHardcodedElsewhere()
    {
        var provider = ComposeBoth();
        var registry = provider.GetRequiredService<IDiscoveryScannerRegistry>();

        var windowsScanners = registry.Scanners.Where(s => s.Id.StartsWith("windows-", StringComparison.Ordinal)).ToList();
        var linuxScanners = registry.Scanners.Where(s => s.Id.StartsWith("linux-", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(windowsScanners);
        Assert.NotEmpty(linuxScanners);
        Assert.All(windowsScanners, s => Assert.Equal(PlatformSupport.Windows, s.PlatformSupport));
        Assert.All(linuxScanners, s => Assert.Equal(PlatformSupport.Linux, s.PlatformSupport));
    }

    [Fact]
    public void ComposedRegistry_ScannersAreSortedDeterministicallyById_MixingBothPlatforms()
    {
        var provider = ComposeBoth();
        var registry = provider.GetRequiredService<IDiscoveryScannerRegistry>();

        var ids = registry.Scanners.Select(s => s.Id).ToList();
        var sorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.Equal(sorted, ids);
    }

    [Fact]
    public void DiscoveryEngine_ResolvesSuccessfully_FromTheComposedRegistry()
    {
        var provider = ComposeBoth();

        var engine = provider.GetRequiredService<IDiscoveryEngine>();

        Assert.NotNull(engine);
    }

    [Fact]
    public void WindowsOnlyComposition_NeverRegistersAnyLinuxScanner()
    {
        var provider = new ServiceCollection().AddLogging().AddServerSleuthInfrastructure().AddServerSleuthWindows().BuildServiceProvider();

        var scanners = provider.GetServices<IDiscoveryScanner>().ToList();

        Assert.DoesNotContain(scanners, s => s.Id.StartsWith("linux-", StringComparison.Ordinal));
        Assert.All(scanners, s => Assert.Equal(PlatformSupport.Windows, s.PlatformSupport));
    }

    [Fact]
    public void LinuxOnlyComposition_NeverRegistersAnyWindowsScanner()
    {
        var provider = new ServiceCollection().AddLogging().AddServerSleuthInfrastructure().AddServerSleuthLinux().BuildServiceProvider();

        var scanners = provider.GetServices<IDiscoveryScanner>().ToList();

        Assert.DoesNotContain(scanners, s => s.Id.StartsWith("windows-", StringComparison.Ordinal));
        Assert.All(scanners, s => Assert.Equal(PlatformSupport.Linux, s.PlatformSupport));
    }
}
