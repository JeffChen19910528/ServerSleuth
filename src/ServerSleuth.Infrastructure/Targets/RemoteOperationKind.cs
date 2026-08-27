namespace ServerSleuth.Infrastructure.Targets;

/// <summary>
/// The closed vocabulary of discovery operations a future remote transport (SSH/WinRM) is
/// permitted to carry — see skill.md (Phase 10D-1) §4. Deliberately sized to match exactly
/// what <see cref="ITargetTransport"/> already exposes today (<see cref="Process.IProcessRunner"/>
/// and <see cref="FileSystem.IFileSystemReader"/>), not the larger example list skill.md's own
/// instructions offer (Registry/WMI/Network/Service queries) — those map to APIs
/// (<c>Microsoft.Win32.Registry</c>, <c>System.Management</c>, <c>ServiceController</c>) that
/// scanners call directly today, with no cross-platform Infrastructure abstraction to classify
/// yet. Extending this vocabulary to cover them is explicit future work — see
/// ARCHITECTURE.md's Phase 10D-1 addendum ("Filesystem/Process Mapping" and "Known
/// Limitations").
///
/// This is a classification for AUDITING/allow-listing which category of operation a structured
/// <see cref="RemoteOperation"/> represents — it is never a free-text command, and no member of
/// this enum stands for "run whatever string is provided" (skill.md §3, §17).
/// </summary>
public enum RemoteOperationKind
{
    /// <summary>A single, discrete process invocation — maps to <see cref="Process.IProcessRunner"/>.
    /// Executable and Arguments stay separate on <see cref="RemoteOperation"/>, exactly as
    /// <see cref="Process.ProcessRequest"/> already keeps them separate today.</summary>
    ProcessQuery,

    /// <summary>Reading one known file's existence/content/metadata — maps to the
    /// non-enumerating members of <see cref="FileSystem.IFileSystemReader"/>
    /// (<c>Exists</c>/<c>ReadTextAsync</c>/<c>ReadBytesAsync</c>/<c>GetFileInfo</c>).</summary>
    FileRead,

    /// <summary>Listing a directory's immediate files/subdirectories — maps to
    /// <see cref="FileSystem.IFileSystemReader"/>'s <c>EnumerateFiles</c>/<c>EnumerateDirectories</c>.
    /// Kept distinct from <see cref="FileRead"/> because enumerating a subtree is a materially
    /// different (broader) operation to authorize than reading one named file.</summary>
    DirectoryQuery
}
