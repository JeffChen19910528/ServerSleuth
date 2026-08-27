using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Networking;
using ServerSleuth.Infrastructure.Runtimes;
using ServerSleuth.Linux.Configuration;
using ServerSleuth.Linux.Containers;
using ServerSleuth.Linux.Cron;
using ServerSleuth.Linux.Kubernetes;
using ServerSleuth.Linux.Native;
using ServerSleuth.Linux.Networking;
using ServerSleuth.Linux.OperatingSystem;
using ServerSleuth.Linux.Packages;
using ServerSleuth.Linux.Process;
using ServerSleuth.Linux.Runtimes;
using ServerSleuth.Linux.Runtimes.Detectors;
using ServerSleuth.Linux.Systemd;

namespace ServerSleuth.Linux;

/// <summary>Registers every Linux-specific abstraction and all Phase 6A/6B
/// <see cref="IDiscoveryScanner"/> implementations. Independent of `AddServerSleuthWindows()` —
/// registering both in the same host is harmless (each scanner's `PlatformSupport` flag is what
/// keeps them from running on the wrong OS), but neither is required by the other.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServerSleuthLinux(this IServiceCollection services)
    {
        // Phase 6A — foundation
        services.AddSingleton<IProcProvider, LinuxProcProvider>();
        services.AddSingleton<ISocketOwnershipResolver, SocketOwnershipResolver>();
        services.AddSingleton<IPortInspector, LinuxPortInspector>();
        services.AddSingleton<ISystemdProvider, SystemctlProvider>();

        // Registered as their concrete types (not just IDiscoveryScanner) so
        // LinuxConfigurationScanner/LinuxNativeDependencyScanner can share the same instances
        // rather than re-registering second copies — they reuse these scanners' output.
        services.AddSingleton<LinuxProcessScanner>();
        services.AddSingleton<LinuxSystemdServiceScanner>();

        services.AddSingleton<IDiscoveryScanner, LinuxOsScanner>();
        services.AddSingleton<IDiscoveryScanner>(sp => sp.GetRequiredService<LinuxProcessScanner>());
        services.AddSingleton<IDiscoveryScanner, LinuxPortScanner>();
        services.AddSingleton<IDiscoveryScanner>(sp => sp.GetRequiredService<LinuxSystemdServiceScanner>());

        // Phase 6B — packages, runtimes, cron
        services.AddSingleton<IPackageManagerProvider, DpkgPackageProvider>();
        services.AddSingleton<IPackageManagerProvider, RpmPackageProvider>();
        services.AddSingleton<IPackageManagerProvider, ApkPackageProvider>();
        services.AddSingleton<IDiscoveryScanner, LinuxPackageScanner>();

        services.AddSingleton<IExecutableLocator, LinuxExecutableLocator>();
        services.AddSingleton<IRuntimeDetector, DotNetRuntimeDetector>();
        services.AddSingleton<IRuntimeDetector, DotNetSdkDetector>();
        services.AddSingleton<IRuntimeDetector, JavaDetector>();
        services.AddSingleton<IRuntimeDetector, PythonDetector>();
        services.AddSingleton<IRuntimeDetector, NodeDetector>();
        services.AddSingleton<IRuntimeDetector, PhpDetector>();
        services.AddSingleton<IRuntimeDetector, GoDetector>();
        services.AddSingleton<LinuxRuntimeDiscoveryScanner>();
        services.AddSingleton<IDiscoveryScanner>(sp => sp.GetRequiredService<LinuxRuntimeDiscoveryScanner>());

        services.AddSingleton<LinuxScheduledTaskScanner>();
        services.AddSingleton<IDiscoveryScanner>(sp => sp.GetRequiredService<LinuxScheduledTaskScanner>());

        // Phase 6C — container runtime discovery
        services.AddSingleton<IContainerRuntimeProvider, DockerContainerRuntimeProvider>();
        services.AddSingleton<IContainerRuntimeProvider, PodmanContainerRuntimeProvider>();
        services.AddSingleton<IDiscoveryScanner, LinuxContainerScanner>();

        // Phase 6D — Kubernetes discovery
        services.AddSingleton<IKubernetesProvider, KubectlKubernetesProvider>();
        services.AddSingleton<IDiscoveryScanner, LinuxKubernetesScanner>();

        // Phase 6E — configuration discovery
        services.AddSingleton<IDiscoveryScanner, LinuxConfigurationScanner>();

        // Phase 6F — native (ELF) dependency discovery
        services.AddSingleton<ILinuxElfParser, ElfParser>();
        services.AddSingleton<ILibraryResolver, LinuxLibraryResolver>();
        services.AddSingleton<ILdconfigProvider, LdconfigProvider>();
        services.AddSingleton<IDiscoveryScanner, LinuxNativeDependencyScanner>();

        return services;
    }
}
