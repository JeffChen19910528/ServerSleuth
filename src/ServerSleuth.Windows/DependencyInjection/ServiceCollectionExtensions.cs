using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Networking;
using ServerSleuth.Infrastructure.Runtimes;
using ServerSleuth.Windows.Binaries;
using ServerSleuth.Windows.Certificates;
using ServerSleuth.Windows.COM;
using ServerSleuth.Windows.Common;
using ServerSleuth.Windows.Configuration;
using ServerSleuth.Windows.IIS;
using ServerSleuth.Windows.Networking;
using ServerSleuth.Windows.OperatingSystem;
using ServerSleuth.Windows.Process;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Windows.Remote;
using ServerSleuth.Windows.Runtimes;
using ServerSleuth.Windows.Runtimes.Detectors;
using ServerSleuth.Windows.ScheduledTasks;
using ServerSleuth.Windows.Services;
using ServerSleuth.Windows.Software;

namespace ServerSleuth.Windows.DependencyInjection;

/// <summary>
/// Minimal Windows scanner registration for Phase 3 — registers every Windows-specific
/// abstraction plus all five Windows IDiscoveryScanner implementations built so far. A full
/// plugin/registry system is deliberately out of scope here; the Cli project (Phase 9) is
/// where real orchestration/profile-selection will consume the registered IDiscoveryScanner
/// collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServerSleuthWindows(this IServiceCollection services)
    {
        services.AddSingleton<IWindowsRegistryReader, WindowsRegistryReader>();
        services.AddSingleton<IProcessEnumerator, ProcessEnumerator>();
        services.AddSingleton<IProcessWmiProvider, ProcessWmiProvider>();
        services.AddSingleton<INetworkTableProvider, NetworkTableProvider>();
        services.AddSingleton<IProcessNameResolver, ProcessNameResolver>();
        services.AddSingleton<IPortInspector, WindowsPortInspector>();
        services.AddSingleton<IServiceEnumerator, ServiceEnumerator>();
        services.AddSingleton<IIisConfigurationProvider, IisConfigurationProvider>();
        services.AddSingleton<IFileVersionMetadataReader, FileVersionMetadataReader>();
        services.AddSingleton<ITaskSchedulerProvider, TaskSchedulerProvider>();
        services.AddSingleton<ICertificateStoreProvider, CertificateStoreProvider>();
        services.AddSingleton<IExecutableLocator, ExecutableLocator>();
        services.AddSingleton<IPeAnalyzer, PeAnalyzer>();

        services.AddSingleton<IRuntimeDetector, DotNetFrameworkDetector>();
        services.AddSingleton<IRuntimeDetector, DotNetRuntimeDetector>();
        services.AddSingleton<IRuntimeDetector, DotNetSdkDetector>();
        services.AddSingleton<IRuntimeDetector, JavaDetector>();
        services.AddSingleton<IRuntimeDetector, PythonDetector>();
        services.AddSingleton<IRuntimeDetector, NodeDetector>();
        services.AddSingleton<IRuntimeDetector, PhpDetector>();
        services.AddSingleton<IRuntimeDetector, GoDetector>();

        // Registered as their concrete types (not just IDiscoveryScanner) so
        // WindowsConfigurationScanner can share the same instance rather than re-registering
        // a second copy — it reuses these scanners' output to derive scan roots.
        services.AddSingleton<IisScanner>();
        services.AddSingleton<WindowsServiceScanner>();
        services.AddSingleton<WindowsScheduledTaskScanner>();
        services.AddSingleton<WindowsComScanner>();

        services.AddSingleton<IDiscoveryScanner, WindowsOsScanner>();
        services.AddSingleton<IDiscoveryScanner, WindowsProcessScanner>();
        services.AddSingleton<IDiscoveryScanner, WindowsPortScanner>();
        services.AddSingleton<IDiscoveryScanner>(sp => sp.GetRequiredService<WindowsServiceScanner>());
        services.AddSingleton<IDiscoveryScanner, WindowsInstalledSoftwareScanner>();
        services.AddSingleton<IDiscoveryScanner>(sp => sp.GetRequiredService<IisScanner>());
        services.AddSingleton<IDiscoveryScanner>(sp => sp.GetRequiredService<WindowsComScanner>());
        services.AddSingleton<IDiscoveryScanner>(sp => sp.GetRequiredService<WindowsScheduledTaskScanner>());
        services.AddSingleton<IDiscoveryScanner, WindowsCertificateScanner>();
        services.AddSingleton<IDiscoveryScanner, RuntimeDiscoveryScanner>();
        services.AddSingleton<IDiscoveryScanner, WindowsConfigurationScanner>();
        services.AddSingleton<IDiscoveryScanner, WindowsBinaryDiscoveryScanner>();

        return services;
    }

    /// <summary>
    /// The target-aware counterpart to <see cref="AddServerSleuthWindows"/> for a REMOTE
    /// Windows/WinRM scan (skill.md Phase 10D-3B §20-21). Registers the nine local-shaped
    /// interfaces from <paramref name="providerSet"/> (backed by WinRM) instead of their local
    /// implementations, plus an <see cref="UnavailableRemoteFileSystemReader"/> for the three
    /// scanners that also need <see cref="IFileSystemReader"/> for optional path-verification.
    ///
    /// **Deliberately registers FEWER scanners than <see cref="AddServerSleuthWindows"/>
    /// (skill.md §21's "no local fallback" outranks completeness) — three scanners are excluded
    /// outright, not silently degraded, because their core data source has no safe remote
    /// equivalent in this phase:**
    /// <see cref="OperatingSystem.WindowsOsScanner"/> (reads <c>EnvironmentSnapshot.Capture()</c>
    /// — local <c>Environment.*</c> BCL calls with no remote bridge — would report the SCANNING
    /// machine's identity, not the target's), <see cref="Runtimes.RuntimeDiscoveryScanner"/> and
    /// <see cref="Configuration.WindowsConfigurationScanner"/>/<see cref="Binaries.WindowsBinaryDiscoveryScanner"/>
    /// (all depend on <c>IProcessRunner</c> — this phase has no remote Windows process-execution
    /// bridge either, the same disclosed gap as the filesystem one). Documented in
    /// ARCHITECTURE.md's Phase 10D-3B addendum, not hacked around with a partial/wrong answer.
    /// </summary>
    public static IServiceCollection AddServerSleuthWindowsRemote(this IServiceCollection services, WinRmWindowsProviderSet providerSet)
    {
        services.AddSingleton(providerSet.RegistryReader);
        services.AddSingleton(providerSet.ProcessWmiProvider);
        services.AddSingleton(providerSet.ProcessEnumerator);
        services.AddSingleton(providerSet.NetworkTableProvider);
        services.AddSingleton(providerSet.ProcessNameResolver);
        services.AddSingleton(providerSet.ServiceEnumerator);
        services.AddSingleton(providerSet.IisConfigurationProvider);
        services.AddSingleton(providerSet.TaskSchedulerProvider);
        services.AddSingleton(providerSet.CertificateStoreProvider);
        services.AddSingleton<IFileSystemReader, UnavailableRemoteFileSystemReader>();
        services.AddSingleton<IPortInspector, WindowsPortInspector>();
        services.AddSingleton<IFileVersionMetadataReader, FileVersionMetadataReader>();

        services.AddSingleton<IisScanner>();
        services.AddSingleton<WindowsServiceScanner>();
        services.AddSingleton<WindowsScheduledTaskScanner>();
        services.AddSingleton<WindowsComScanner>();

        services.AddSingleton<IDiscoveryScanner, WindowsProcessScanner>();
        services.AddSingleton<IDiscoveryScanner, WindowsPortScanner>();
        services.AddSingleton<IDiscoveryScanner>(sp => sp.GetRequiredService<WindowsServiceScanner>());
        services.AddSingleton<IDiscoveryScanner, WindowsInstalledSoftwareScanner>();
        services.AddSingleton<IDiscoveryScanner>(sp => sp.GetRequiredService<IisScanner>());
        services.AddSingleton<IDiscoveryScanner>(sp => sp.GetRequiredService<WindowsComScanner>());
        services.AddSingleton<IDiscoveryScanner>(sp => sp.GetRequiredService<WindowsScheduledTaskScanner>());
        services.AddSingleton<IDiscoveryScanner, WindowsCertificateScanner>();

        return services;
    }
}
