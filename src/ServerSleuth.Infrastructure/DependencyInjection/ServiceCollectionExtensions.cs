using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Infrastructure.DependencyInjection;

/// <summary>
/// Registers only the cross-platform infrastructure abstractions that exist so far.
/// IPortInspector is intentionally not registered here — it has no concrete implementation
/// until Phase 3 (Windows) / Phase 6 (Linux) provide one.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the cross-platform infrastructure abstractions. Phase 10D-2 §20: when
    /// <paramref name="remoteTransport"/> is supplied (a caller — the CLI composition root —
    /// already resolved a real remote target and built its transport, e.g. via
    /// <c>RemoteTargetTransportFactory.CreateSsh</c>), <see cref="IProcessRunner"/>/
    /// <see cref="IFileSystemReader"/> are registered as THAT transport's own instances instead
    /// of the local ones — so every Linux provider/scanner that only ever depends on those two
    /// interfaces (never on <see cref="ITargetTransport"/> directly) is automatically wired to
    /// the remote target, with zero change to any provider/scanner. This is the one seam where
    /// target selection happens — never inside <c>DiscoveryEngine</c>/Analysis/a scanner itself
    /// (skill.md §20-21).
    /// </summary>
    public static IServiceCollection AddServerSleuthInfrastructure(this IServiceCollection services, ITargetTransport? remoteTransport = null)
    {
        services.AddSingleton<ISecretRedactor, SecretRedactor>();

        if (remoteTransport is null)
        {
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<IFileSystemReader, FileSystemReader>();

            // Phase 10C §3, §9: local scanning is now explicitly represented as a target.
            // Platform is resolved once here, from the current process's own runtime — never
            // probed over a network.
            services.AddSingleton<ITargetTransport>(sp => new LocalTargetTransport(
                ScanTarget.Local(ResolveLocalPlatform()),
                sp.GetRequiredService<IProcessRunner>(),
                sp.GetRequiredService<IFileSystemReader>()));
        }
        else
        {
            services.AddSingleton(remoteTransport);
            services.AddSingleton<ITargetTransport>(remoteTransport);
            services.AddSingleton(remoteTransport.ProcessRunner);
            services.AddSingleton(remoteTransport.FileSystemReader);
        }

        return services;
    }

    private static TargetPlatform ResolveLocalPlatform() =>
        OperatingSystem.IsWindows() ? TargetPlatform.Windows :
        OperatingSystem.IsLinux() ? TargetPlatform.Linux :
        TargetPlatform.Unknown;

    /// <summary>
    /// Registers the platform-neutral cross-platform discovery orchestration layer (Phase 6G)
    /// — <see cref="IDiscoveryScannerRegistry"/>/<see cref="IDiscoveryEngine"/>. Composed
    /// separately from <see cref="AddServerSleuthInfrastructure"/> (rather than folded into it)
    /// because it must run AFTER every platform's own `AddServerSleuthWindows()`/
    /// `AddServerSleuthLinux()` has already registered its <see cref="Core.Interfaces.IDiscoveryScanner"/>
    /// implementations, so the registry actually has scanners to enumerate.
    /// </summary>
    public static IServiceCollection AddServerSleuthDiscoveryEngine(this IServiceCollection services)
    {
        services.AddSingleton<IDiscoveryScannerRegistry, DiscoveryScannerRegistry>();
        services.AddSingleton<IDiscoveryEngine, DiscoveryEngine>();

        return services;
    }
}
