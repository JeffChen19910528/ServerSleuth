using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Cli.Composition;
using ServerSleuth.Cli.Options;
using ServerSleuth.Core.Orchestration;

namespace ServerSleuth.Cli.Tests;

/// <summary>
/// Platform composition — see skill.md (Phase 10A) §4, §9, §23, (Phase 10D-2) §20. Verifies the
/// CLI's own composition root resolves
/// <see cref="IDiscoveryEngine"/>/<see cref="IDiscoveryScannerRegistry"/> without throwing, using
/// the existing DI registration methods (never a second/duplicate scanner registration built by
/// this test project). All local-target scenarios here pass a bare <see cref="ScanOptions"/>
/// (<c>Remote</c> unset) — remote/SSH composition is covered separately by
/// <c>RemoteCompositionTests</c>.
/// </summary>
public class CompositionRootTests
{
    private static readonly ScanOptions LocalOptions = new();

    [Fact]
    public void Build_ComposesSuccessfully_OnTheCurrentPlatform()
    {
        using var provider = (ServiceProvider)CompositionRoot.Build(LocalOptions);

        var registry = provider.GetRequiredService<IDiscoveryScannerRegistry>();
        var engine = provider.GetRequiredService<IDiscoveryEngine>();

        Assert.NotNull(registry);
        Assert.NotNull(engine);
    }

    [Fact]
    public void Build_RegistersAtLeastOneScanner_OnAKnownPlatform()
    {
        // Only meaningful when THIS build actually has a matching platform registration
        // compiled in — the plain net8.0 TFM (no SERVERSLEUTH_WINDOWS symbol) has no Windows
        // scanners compiled in at all, so running it on a Windows test host legitimately
        // registers zero scanners; that build is meant to run on Linux, where the Linux branch
        // fires instead. See ServerSleuth.Cli.csproj's own multi-targeting doc comment.
#if SERVERSLEUTH_WINDOWS
        var platformRegistrationIsCompiledInForThisOs = OperatingSystem.IsWindows();
#else
        var platformRegistrationIsCompiledInForThisOs = OperatingSystem.IsLinux();
#endif
        if (!platformRegistrationIsCompiledInForThisOs)
        {
            return;
        }

        using var provider = (ServiceProvider)CompositionRoot.Build(LocalOptions);
        var registry = provider.GetRequiredService<IDiscoveryScannerRegistry>();

        Assert.NotEmpty(registry.Scanners);
    }

    [Fact]
    public void Build_NeverRegistersTheSameScannerIdTwice()
    {
        using var provider = (ServiceProvider)CompositionRoot.Build(LocalOptions);
        var registry = provider.GetRequiredService<IDiscoveryScannerRegistry>();

        var ids = registry.Scanners.Select(s => s.Id).ToList();
        Assert.Equal(ids.Distinct(StringComparer.Ordinal).Count(), ids.Count);
    }

    [Fact]
    public void Build_CanBeCalledMultipleTimes_EachProducingAnIndependentProvider()
    {
        using var first = (ServiceProvider)CompositionRoot.Build(LocalOptions);
        using var second = (ServiceProvider)CompositionRoot.Build(LocalOptions);

        Assert.NotSame(first, second);
        Assert.NotSame(
            first.GetRequiredService<IDiscoveryScannerRegistry>(),
            second.GetRequiredService<IDiscoveryScannerRegistry>());
    }
}
