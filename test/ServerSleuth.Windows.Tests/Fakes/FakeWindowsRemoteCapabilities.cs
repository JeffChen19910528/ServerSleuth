using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Certificates;
using ServerSleuth.Windows.IIS;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Windows.Remote;
using ServerSleuth.Windows.ScheduledTasks;
using ServerSleuth.Windows.Wmi;

namespace ServerSleuth.Windows.Tests.Fakes;

/// <summary>
/// A deterministic, in-memory-only <see cref="IWindowsRemoteCapabilities"/> double — see
/// skill.md (Phase 10D-3A) §18. Records every query it receives so a test can assert on the
/// exact structured request that was passed, WITHOUT ever touching the registry/WMI/IIS/Task
/// Scheduler/certificate stores, a network socket, or any other real resource. Every method
/// returns a canned <see cref="OperationStatus.Success"/> result with an empty payload.
/// </summary>
public sealed class FakeWindowsRemoteCapabilities : IWindowsRemoteCapabilities
{
    public ScanTarget Target { get; }

    public IWindowsRemoteRegistryOperations Registry { get; }
    public IWindowsRemoteWmiOperations Wmi { get; }
    public IWindowsRemoteIisOperations Iis { get; }
    public IWindowsRemoteTaskSchedulerOperations TaskScheduler { get; }
    public IWindowsRemoteCertificateOperations Certificates { get; }

    public List<WindowsRegistryQuery> RecordedRegistryQueries { get; } = [];
    public List<WindowsWmiQuery> RecordedWmiQueries { get; } = [];
    public List<CertificateStoreSource> RecordedCertificateQueries { get; } = [];
    public int IisSnapshotCallCount { get; private set; }
    public int TaskSchedulerSnapshotCallCount { get; private set; }

    public FakeWindowsRemoteCapabilities(ScanTarget? target = null)
    {
        Target = target ?? ScanTarget.Remote("windows-host.example.internal", TargetPlatform.Windows);
        Registry = new RegistryOps(this);
        Wmi = new WmiOps(this);
        Iis = new IisOps(this);
        TaskScheduler = new TaskSchedulerOps(this);
        Certificates = new CertificateOps(this);
    }

    private sealed class RegistryOps(FakeWindowsRemoteCapabilities owner) : IWindowsRemoteRegistryOperations
    {
        public ScanTarget Target => owner.Target;

        public WindowsRemoteOperationResult<WindowsRegistryQueryResult> Query(WindowsRegistryQuery query)
        {
            owner.RecordedRegistryQueries.Add(query);
            return WindowsRemoteOperationResult<WindowsRegistryQueryResult>.Ok(new WindowsRegistryQueryResult());
        }
    }

    private sealed class WmiOps(FakeWindowsRemoteCapabilities owner) : IWindowsRemoteWmiOperations
    {
        public ScanTarget Target => owner.Target;

        public WindowsRemoteOperationResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>> Query(WindowsWmiQuery query)
        {
            owner.RecordedWmiQueries.Add(query);
            return WindowsRemoteOperationResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>.Ok([]);
        }
    }

    private sealed class IisOps(FakeWindowsRemoteCapabilities owner) : IWindowsRemoteIisOperations
    {
        public ScanTarget Target => owner.Target;

        public WindowsRemoteOperationResult<IisSnapshot> GetSnapshot()
        {
            owner.IisSnapshotCallCount++;
            return WindowsRemoteOperationResult<IisSnapshot>.Ok(new IisSnapshot());
        }
    }

    private sealed class TaskSchedulerOps(FakeWindowsRemoteCapabilities owner) : IWindowsRemoteTaskSchedulerOperations
    {
        public ScanTarget Target => owner.Target;

        public WindowsRemoteOperationResult<IReadOnlyList<ScheduledTaskRow>> GetSnapshot()
        {
            owner.TaskSchedulerSnapshotCallCount++;
            return WindowsRemoteOperationResult<IReadOnlyList<ScheduledTaskRow>>.Ok([]);
        }
    }

    private sealed class CertificateOps(FakeWindowsRemoteCapabilities owner) : IWindowsRemoteCertificateOperations
    {
        public ScanTarget Target => owner.Target;

        public WindowsRemoteOperationResult<IReadOnlyList<CertificateRow>> Query(CertificateStoreSource source)
        {
            owner.RecordedCertificateQueries.Add(source);
            return WindowsRemoteOperationResult<IReadOnlyList<CertificateRow>>.Ok([]);
        }
    }
}
