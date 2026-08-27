using ServerSleuth.Core.Targets;
using ServerSleuth.Windows.Certificates;
using ServerSleuth.Windows.IIS;
using ServerSleuth.Windows.Networking;
using ServerSleuth.Windows.Process;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Windows.ScheduledTasks;
using ServerSleuth.Windows.Services;
using ServerSleuth.Windows.Wmi;

namespace ServerSleuth.Windows.Remote;

/// <summary>
/// The target-aware composition seam (skill.md Phase 10D-3B §20): one instance of every
/// EXISTING local Windows interface (<see cref="IWindowsRegistryReader"/>/<see cref="IProcessWmiProvider"/>/
/// <see cref="IProcessEnumerator"/>/<see cref="INetworkTableProvider"/>/<see cref="IProcessNameResolver"/>/
/// <see cref="IServiceEnumerator"/>/<see cref="IIisConfigurationProvider"/>/<see cref="ITaskSchedulerProvider"/>/
/// <see cref="ICertificateStoreProvider"/>), all backed by the SAME <see cref="WinRmWindowsRemoteCapabilities"/>
/// (and therefore the same one <see cref="CimWinRmTransport"/>/<see cref="ICimSession"/>).
///
/// This is the Windows-domain counterpart to Phase 10D-2's finding that "the real seam for
/// target-aware behavior was always the COMPOSITION ROOT's registration of [the local
/// interfaces], not anything inside a scanner" — every one of the nine interfaces below is
/// exactly what <c>AddServerSleuthWindows()</c> already registers for a LOCAL scan; this type
/// exists so <c>CompositionRoot</c> can register these REMOTE instances instead, with zero
/// scanner/provider code change anywhere (mirroring <c>AddServerSleuthInfrastructure</c>'s own
/// optional-<c>ITargetTransport</c>-parameter pattern for Linux/SSH).
/// </summary>
public sealed class WinRmWindowsProviderSet : IDisposable
{
    private readonly WinRmWindowsRemoteCapabilities _capabilities;

    public ScanTarget Target => _capabilities.Target;

    public IWindowsRegistryReader RegistryReader { get; }
    public IProcessWmiProvider ProcessWmiProvider { get; }
    public IProcessEnumerator ProcessEnumerator { get; }
    public INetworkTableProvider NetworkTableProvider { get; }
    public IProcessNameResolver ProcessNameResolver { get; }
    public IServiceEnumerator ServiceEnumerator { get; }
    public IIisConfigurationProvider IisConfigurationProvider { get; }
    public ITaskSchedulerProvider TaskSchedulerProvider { get; }
    public ICertificateStoreProvider CertificateStoreProvider { get; }

    /// <summary>Built from an already-constructed <see cref="WinRmWindowsRemoteCapabilities"/> —
    /// normally obtained via <see cref="WindowsRemoteCapabilityFactory.CreateWinRm"/>, the SAME
    /// entry point <c>CompositionRoot</c> uses, so there is exactly one place a
    /// <see cref="ICimSession"/> gets constructed for a given target, never two independent
    /// sessions racing each other.</summary>
    public WinRmWindowsProviderSet(WinRmWindowsRemoteCapabilities capabilities)
    {
        _capabilities = capabilities;

        var registryOperations = (WinRmRegistryOperations)_capabilities.Registry;
        var wmiOperations = (WinRmWmiOperations)_capabilities.Wmi;

        RegistryReader = new WinRmWindowsRegistryReader(registryOperations);
        ProcessWmiProvider = new WinRmProcessWmiProvider(wmiOperations);
        ProcessEnumerator = new WinRmProcessEnumerator(wmiOperations);
        NetworkTableProvider = new WinRmNetworkTableProvider(wmiOperations);
        ProcessNameResolver = new WinRmProcessNameResolver(wmiOperations);
        ServiceEnumerator = new WinRmServiceEnumerator(wmiOperations);
        IisConfigurationProvider = new WinRmIisConfigurationProvider((WinRmIisOperations)_capabilities.Iis);
        TaskSchedulerProvider = new WinRmTaskSchedulerProvider((WinRmTaskSchedulerOperations)_capabilities.TaskScheduler);
        CertificateStoreProvider = new WinRmCertificateStoreProvider((WinRmCertificateOperations)_capabilities.Certificates);
    }

    public void Connect(CancellationToken cancellationToken) => _capabilities.Connect(cancellationToken);

    public void Dispose() => _capabilities.Dispose();
}
