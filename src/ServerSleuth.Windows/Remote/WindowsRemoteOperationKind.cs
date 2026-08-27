namespace ServerSleuth.Windows.Remote;

/// <summary>
/// The closed vocabulary of Windows-specific discovery operations a future WinRM transport is
/// permitted to carry — see skill.md (Phase 10D-3A) §5, the Windows-domain counterpart to
/// <see cref="ServerSleuth.Infrastructure.Targets.RemoteOperationKind"/>. Deliberately a
/// SEPARATE enum, not an extension of that cross-platform one: Phase 10D-2's
/// <c>RemoteOperationKind</c> (<c>ProcessQuery</c>/<c>FileRead</c>/<c>DirectoryQuery</c>) maps
/// 1:1 onto <c>IProcessRunner</c>/<c>IFileSystemReader</c> — generic, OS-agnostic primitives the
/// Linux SSH transport already implements unchanged. Registry/WMI/IIS/Task-Scheduler/Certificate
/// concepts have no such generic shape; they are Windows-domain concepts that belong in
/// <c>ServerSleuth.Windows</c> (the same project that already owns
/// <c>IWindowsRegistryReader</c>/<c>IProcessWmiProvider</c>/etc.), exactly like the project
/// layout in the repository's own CLAUDE.md already says ("platform-specific code must never
/// leak into Core" — the converse also holds: cross-platform Infrastructure must never absorb
/// Windows-only vocabulary). Extending the Linux/SSH-era enum would also have required touching
/// Phase 10D-2 code, which this phase's own instructions explicitly forbid.
///
/// Sized to exactly five members, not the seven skill.md §5 offered as a starting menu:
/// <c>ServiceQuery</c> was folded into <see cref="WmiQuery"/> (Windows service state is fully
/// representable as a <c>Win32_Service</c> WMI class query — see
/// <see cref="ServerSleuth.Windows.Wmi.WindowsWmiQuery"/>'s doc comment) and
/// <c>ComRegistryQuery</c> was folded into <see cref="RegistryQuery"/> (COM registration is,
/// today, ALREADY read entirely through <c>IWindowsRegistryReader</c> — see
/// <c>ComClsidReader</c> — so it needs no vocabulary of its own). Both decisions are documented
/// in ARCHITECTURE.md's Phase 10D-3A addendum.
///
/// Like its cross-platform counterpart, this is a classification for AUDITING which category of
/// operation a structured request represents — never a free-text command, and no member stands
/// for "run whatever the caller provides" (skill.md §4, §17).
/// </summary>
public enum WindowsRemoteOperationKind
{
    /// <summary>Read-only registry key/value access — maps to
    /// <see cref="ServerSleuth.Windows.Registry.IWindowsRegistryReader"/> and, by extension, to
    /// every Windows scanner that already reads the registry exclusively through it (installed
    /// software, service detail, COM registration).</summary>
    RegistryQuery,

    /// <summary>A structured, property-select-plus-equality-filter WMI class query — maps to
    /// <see cref="ServerSleuth.Windows.Process.IProcessWmiProvider"/> and
    /// <see cref="ServerSleuth.Windows.Networking.INetworkTableProvider"/> today, and is sized
    /// to also represent a future <c>Win32_Service</c> query (see above). Never an arbitrary WQL
    /// string and never a WMI method invocation — see
    /// <see cref="ServerSleuth.Windows.Wmi.WindowsWmiQuery"/>.</summary>
    WmiQuery,

    /// <summary>The full live IIS configuration snapshot (sites/bindings/applications/pools) —
    /// maps to <see cref="ServerSleuth.Windows.IIS.IIisConfigurationProvider"/>. Not registry-
    /// or WMI-shaped (backed by Microsoft.Web.Administration, which reads
    /// <c>applicationHost.config</c> through IIS's own configuration system), so it needs its
    /// own vocabulary entry.</summary>
    IisQuery,

    /// <summary>The full registered-task tree — maps to
    /// <see cref="ServerSleuth.Windows.ScheduledTasks.ITaskSchedulerProvider"/>. Backed by the
    /// Task Scheduler 2.0 COM API, distinct from both registry and WMI.</summary>
    TaskSchedulerQuery,

    /// <summary>Public-only certificate metadata for one certificate store — maps to
    /// <see cref="ServerSleuth.Windows.Certificates.ICertificateStoreProvider"/>. Structurally
    /// incapable of returning private-key material — see
    /// <see cref="ServerSleuth.Windows.Certificates.IWindowsRemoteCertificateOperations"/>'s doc
    /// comment.</summary>
    CertificateQuery
}
