using ServerSleuth.Infrastructure.FileSystem;

namespace ServerSleuth.Infrastructure.Remote;

/// <summary>
/// The thin seam between this codebase and the real SSH library (Renci.SshNet), narrow enough
/// to fake deterministically in tests without a live SSH server — see skill.md (Phase 10D-2)
/// §27. <see cref="SshNetSession"/> is the only real implementation; test doubles implement this
/// directly. Every member maps to a structured, non-shell SSH/SFTP primitive — there is no
/// member here that accepts a raw command string from a caller (only
/// <see cref="ExecuteCommand"/>, which is only ever called with the ALREADY-built, safely-quoted
/// output of <see cref="SshCommandLineBuilder.Build"/>, never a caller-supplied string).
/// </summary>
public interface ISshSession : IDisposable
{
    bool IsConnected { get; }

    SshConnectResult Connect(CancellationToken cancellationToken);

    SshCommandExecutionResult ExecuteCommand(string commandLine, TimeSpan timeout, CancellationToken cancellationToken);

    bool SftpExists(string path);

    FileSystemResult<byte[]> SftpReadBytes(string path);

    FileSystemResult<SshRemoteFileInfo> SftpGetAttributes(string path);

    /// <summary>The symlink target of <paramref name="path"/>, or a non-success result if
    /// <paramref name="path"/> is not a symlink or cannot be read — the remote equivalent of
    /// <c>FileInfo.LinkTarget</c>, needed for <c>/proc/&lt;pid&gt;/exe</c> resolution
    /// (skill.md §11). Deliberately NOT named with an "Sftp" prefix: the real implementation
    /// (<see cref="SshNetSession"/>) cannot satisfy this via SFTP at all — see that type's own
    /// doc comment for why — and falls back to the single well-known, read-only <c>readlink</c>
    /// utility over the same safely-quoted exec path as any other <see cref="RemoteOperationKind.ProcessQuery"/>.</summary>
    FileSystemResult<string> ReadLinkTarget(string path);

    FileSystemResult<IReadOnlyList<SshRemoteFileInfo>> SftpListDirectory(string path);
}
