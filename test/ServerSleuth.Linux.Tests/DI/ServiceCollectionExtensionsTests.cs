using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.DependencyInjection;
using ServerSleuth.Infrastructure.Networking;
using ServerSleuth.Infrastructure.Runtimes;
using ServerSleuth.Linux;
using ServerSleuth.Linux.Containers;
using ServerSleuth.Linux.Cron;
using ServerSleuth.Linux.Kubernetes;
using ServerSleuth.Linux.Native;
using ServerSleuth.Linux.Networking;
using ServerSleuth.Linux.Packages;
using ServerSleuth.Linux.Process;
using ServerSleuth.Linux.Runtimes;
using ServerSleuth.Linux.Systemd;

namespace ServerSleuth.Linux.Tests.DI;

public class ServiceCollectionExtensionsTests
{
    /// <summary>`AddServerSleuthLinux()` intentionally only registers Linux-specific pieces —
    /// it relies on the shared `AddServerSleuthInfrastructure()` (Phase 2) and logging having
    /// already been registered by the composition root, exactly as `AddServerSleuthWindows()`
    /// already does. These tests compose that same way rather than expecting Linux's DI method
    /// to duplicate Infrastructure's own registrations.</summary>
    private static IServiceCollection BaseServices() =>
        new ServiceCollection().AddLogging().AddServerSleuthInfrastructure();

    [Fact]
    public void AddServerSleuthLinux_RegistersAllElevenScanners()
    {
        var services = BaseServices().AddServerSleuthLinux().BuildServiceProvider();

        var scanners = services.GetServices<IDiscoveryScanner>().ToList();

        Assert.Equal(11, scanners.Count);
        Assert.Contains(scanners, s => s.Id == "linux-os-scanner");
        Assert.Contains(scanners, s => s.Id == "linux-process-scanner");
        Assert.Contains(scanners, s => s.Id == "linux-port-scanner");
        Assert.Contains(scanners, s => s.Id == "linux-systemd-service-scanner");
        Assert.Contains(scanners, s => s.Id == "linux-package-scanner");
        Assert.Contains(scanners, s => s.Id == "linux-runtime-discovery-scanner");
        Assert.Contains(scanners, s => s.Id == "linux-scheduled-task-scanner");
        Assert.Contains(scanners, s => s.Id == "linux-container-scanner");
        Assert.Contains(scanners, s => s.Id == "linux-kubernetes-scanner");
        Assert.Contains(scanners, s => s.Id == "linux-configuration-scanner");
        Assert.Contains(scanners, s => s.Id == "linux-native-dependency-scanner");
    }

    [Fact]
    public void AddServerSleuthLinux_ProcessSystemdRuntimeAndScheduledTaskScanners_ResolveAsConcreteTypesToo()
    {
        // LinuxConfigurationScanner and LinuxNativeDependencyScanner depend on these concrete
        // types directly (to reuse their output for scan-root derivation / executable-path
        // gathering), so they must be resolvable both ways without being registered — and
        // therefore instantiated — twice.
        var services = BaseServices().AddServerSleuthLinux().BuildServiceProvider();

        var processScanner = services.GetRequiredService<LinuxProcessScanner>();
        var systemdScanner = services.GetRequiredService<LinuxSystemdServiceScanner>();
        var cronScanner = services.GetRequiredService<LinuxScheduledTaskScanner>();
        var runtimeScanner = services.GetRequiredService<LinuxRuntimeDiscoveryScanner>();

        var discoveryScanners = services.GetServices<IDiscoveryScanner>();
        Assert.Contains(discoveryScanners, s => ReferenceEquals(s, processScanner));
        Assert.Contains(discoveryScanners, s => ReferenceEquals(s, systemdScanner));
        Assert.Contains(discoveryScanners, s => ReferenceEquals(s, cronScanner));
        Assert.Contains(discoveryScanners, s => ReferenceEquals(s, runtimeScanner));
    }

    [Fact]
    public void AddServerSleuthLinux_AllRegisteredScanners_ReportLinuxPlatformSupport()
    {
        var services = BaseServices().AddServerSleuthLinux().BuildServiceProvider();

        var scanners = services.GetServices<IDiscoveryScanner>();

        Assert.All(scanners, s => Assert.Equal(PlatformSupport.Linux, s.PlatformSupport));
    }

    [Fact]
    public void AddServerSleuthLinux_RegistersSupportingAbstractions()
    {
        var services = BaseServices().AddServerSleuthLinux().BuildServiceProvider();

        Assert.IsType<LinuxProcProvider>(services.GetRequiredService<IProcProvider>());
        Assert.IsType<SocketOwnershipResolver>(services.GetRequiredService<ISocketOwnershipResolver>());
        Assert.IsType<LinuxPortInspector>(services.GetRequiredService<IPortInspector>());
        Assert.IsType<SystemctlProvider>(services.GetRequiredService<ISystemdProvider>());
        Assert.IsType<LinuxExecutableLocator>(services.GetRequiredService<IExecutableLocator>());
    }

    [Fact]
    public void AddServerSleuthLinux_RegistersThreePackageManagerProvidersAndSevenRuntimeDetectors()
    {
        var services = BaseServices().AddServerSleuthLinux().BuildServiceProvider();

        Assert.Equal(3, services.GetServices<IPackageManagerProvider>().Count());
        Assert.Equal(7, services.GetServices<IRuntimeDetector>().Count());
    }

    [Fact]
    public void AddServerSleuthLinux_RegistersTwoContainerRuntimeProviders()
    {
        var services = BaseServices().AddServerSleuthLinux().BuildServiceProvider();

        var providers = services.GetServices<IContainerRuntimeProvider>().ToList();

        Assert.Equal(2, providers.Count);
        Assert.Contains(providers, p => p.RuntimeName == "docker");
        Assert.Contains(providers, p => p.RuntimeName == "podman");
    }

    [Fact]
    public void AddServerSleuthLinux_RegistersOneKubernetesProvider()
    {
        var services = BaseServices().AddServerSleuthLinux().BuildServiceProvider();

        Assert.IsType<KubectlKubernetesProvider>(services.GetRequiredService<IKubernetesProvider>());
    }

    [Fact]
    public void AddServerSleuthLinux_RegistersNativeDependencyAbstractions()
    {
        var services = BaseServices().AddServerSleuthLinux().BuildServiceProvider();

        Assert.IsType<ElfParser>(services.GetRequiredService<ILinuxElfParser>());
        Assert.IsType<LinuxLibraryResolver>(services.GetRequiredService<ILibraryResolver>());
        Assert.IsType<LdconfigProvider>(services.GetRequiredService<ILdconfigProvider>());
    }

    [Fact]
    public void AddServerSleuthLinux_OnAnEmptyServiceCollection_RegistersOnlyItsOwnServices()
    {
        var services = new ServiceCollection();
        services.AddServerSleuthLinux();

        // 4 Phase 6A abstractions + 1 concrete LinuxProcessScanner + 1 concrete
        // LinuxSystemdServiceScanner + 3 Phase 6A IDiscoveryScanner registrations (OS/Process
        // factory/Port) + 1 IDiscoveryScanner factory for the systemd scanner + 3 package
        // providers + 1 package scanner + 1 executable locator + 7 runtime detectors + 1
        // concrete LinuxRuntimeDiscoveryScanner + 1 IDiscoveryScanner factory for the runtime
        // scanner + 1 concrete LinuxScheduledTaskScanner + 1 IDiscoveryScanner factory for the
        // cron scanner + 2 container runtime providers + 1 container scanner + 1 kubernetes
        // provider + 1 kubernetes scanner + 1 configuration scanner + 3 native-dependency
        // abstractions (ILinuxElfParser/ILibraryResolver/ILdconfigProvider) + 1 native
        // dependency scanner = 36. No accidental extra services.
        Assert.Equal(36, services.Count);
    }
}
