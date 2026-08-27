using ServerSleuth.Core.Targets;
using ServerSleuth.Windows.Certificates;
using ServerSleuth.Windows.IIS;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Windows.ScheduledTasks;
using ServerSleuth.Windows.Wmi;

namespace ServerSleuth.Windows.Remote;

/// <summary>
/// The real <see cref="IWindowsRemoteCapabilities"/> implementation — fills the seam
/// <see cref="WindowsRemoteCapabilityFactory.Create"/> left always-throwing since Phase 10D-3A.
/// <see cref="Registry"/> and <see cref="Wmi"/> are REAL (backed by the shared
/// <see cref="CimWinRmTransport"/>); <see cref="Iis"/>/<see cref="TaskScheduler"/>/
/// <see cref="Certificates"/> are the disclosed-gap stubs — see each implementation's own doc
/// comment for exactly why. Owns the transport's lifetime: <see cref="Connect"/>/
/// <see cref="Dispose"/> delegate straight through.
/// </summary>
public sealed class WinRmWindowsRemoteCapabilities : IWindowsRemoteCapabilities, IDisposable
{
    private readonly CimWinRmTransport _transport;

    public ScanTarget Target => _transport.Target;

    public IWindowsRemoteRegistryOperations Registry { get; }
    public IWindowsRemoteWmiOperations Wmi { get; }
    public IWindowsRemoteIisOperations Iis { get; }
    public IWindowsRemoteTaskSchedulerOperations TaskScheduler { get; }
    public IWindowsRemoteCertificateOperations Certificates { get; }

    public WinRmWindowsRemoteCapabilities(ScanTarget target, ICimSession session, TimeSpan operationTimeout)
    {
        _transport = new CimWinRmTransport(target, session);
        Registry = new WinRmRegistryOperations(_transport, operationTimeout);
        Wmi = new WinRmWmiOperations(_transport, operationTimeout);
        Iis = new WinRmIisOperations(target);
        TaskScheduler = new WinRmTaskSchedulerOperations(target);
        Certificates = new WinRmCertificateOperations(target);
    }

    public void Connect(CancellationToken cancellationToken) => _transport.Connect(cancellationToken);

    public void Dispose() => _transport.Dispose();
}
