using ServerSleuth.Core.Targets;
using ServerSleuth.Windows.Certificates;
using ServerSleuth.Windows.IIS;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Windows.ScheduledTasks;
using ServerSleuth.Windows.Wmi;

namespace ServerSleuth.Windows.Remote;

/// <summary>
/// The single injection point bundling every Windows remote capability for one
/// <see cref="ScanTarget"/> — the Windows-domain counterpart to
/// <see cref="ServerSleuth.Infrastructure.Targets.ITargetTransport"/>'s
/// <c>Target</c>/<c>ProcessRunner</c>/<c>FileSystemReader</c> bundle. Deliberately five
/// PROPERTIES of five NARROW single-method interfaces, not five methods flattened onto one
/// "WindowsRemoteEverything" interface (skill.md §14's own "avoid interface explosion, but
/// prefer small capability interfaces" balance) — each of
/// <see cref="IWindowsRemoteRegistryOperations"/>/<see cref="IWindowsRemoteWmiOperations"/>/
/// <see cref="IWindowsRemoteIisOperations"/>/<see cref="IWindowsRemoteTaskSchedulerOperations"/>/
/// <see cref="IWindowsRemoteCertificateOperations"/> remains independently implementable,
/// mockable, and testable, exactly mirroring the five existing LOCAL interfaces
/// (<see cref="ServerSleuth.Windows.Registry.IWindowsRegistryReader"/>,
/// <see cref="ServerSleuth.Windows.Process.IProcessWmiProvider"/>,
/// <see cref="IIisConfigurationProvider"/>, <see cref="ITaskSchedulerProvider"/>,
/// <see cref="ICertificateStoreProvider"/>) this whole capability model was designed against.
///
/// No implementation of this interface exists anywhere in this codebase yet — see
/// <see cref="WindowsRemoteCapabilityFactory"/>, the explicit always-throwing seam a future
/// WinRM implementation fills in, mirroring
/// <see cref="ServerSleuth.Infrastructure.Targets.RemoteTargetTransportFactory.Create"/>'s own
/// pre-Phase-10D-2 shape.
/// </summary>
public interface IWindowsRemoteCapabilities
{
    ScanTarget Target { get; }

    IWindowsRemoteRegistryOperations Registry { get; }
    IWindowsRemoteWmiOperations Wmi { get; }
    IWindowsRemoteIisOperations Iis { get; }
    IWindowsRemoteTaskSchedulerOperations TaskScheduler { get; }
    IWindowsRemoteCertificateOperations Certificates { get; }
}
