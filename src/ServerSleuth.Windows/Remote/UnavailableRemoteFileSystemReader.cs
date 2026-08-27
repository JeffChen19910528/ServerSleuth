using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;

namespace ServerSleuth.Windows.Remote;

/// <summary>
/// A safe, inert <see cref="IFileSystemReader"/> for a remote Windows/WinRM scan — see
/// skill.md (Phase 10D-3B) §19, §21. <see cref="WindowsComScanner"/>/<see cref="WindowsScheduledTaskScanner"/>/
/// <see cref="IisScanner"/> all take a HARD <see cref="IFileSystemReader"/> constructor
/// dependency (used only for OPTIONAL physical-path-existence/PE-metadata enrichment — every
/// one of their internal mapping methods already accepts a <c>null</c> reader for "verification
/// unavailable," a pre-existing degraded mode, not something this phase invented). This phase
/// has NO remote filesystem bridge for Windows (no SFTP-equivalent reachable over WS-Man
/// without PowerShell — a disclosed gap, see ARCHITECTURE.md's Phase 10D-3B addendum), so
/// registering the LOCAL <c>FileSystemReader</c> for these scanners during a remote scan would
/// silently check the SCANNING machine's disk instead of the target's — exactly the local
/// fallback skill.md §21 makes mandatory to prevent. This type is the alternative: every member
/// returns a <see cref="OperationStatus.Unsupported"/>/empty result WITHOUT EVER calling
/// <c>System.IO</c> — the affected scanners degrade to "path verification unavailable" rather
/// than reading the wrong machine.
/// </summary>
public sealed class UnavailableRemoteFileSystemReader : IFileSystemReader
{
    public bool Exists(string path) => false;

    public Task<FileSystemResult<string>> ReadTextAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(FileSystemResult<string>.Failure(OperationStatus.Unsupported, "Remote Windows filesystem access is not implemented."));

    public Task<FileSystemResult<byte[]>> ReadBytesAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(FileSystemResult<byte[]>.Failure(OperationStatus.Unsupported, "Remote Windows filesystem access is not implemented."));

    public FileSystemResult<FileEntryInfo> GetFileInfo(string path) => FileSystemResult<FileEntryInfo>.Failure(OperationStatus.Unsupported, "Remote Windows filesystem access is not implemented.");

    public FileSystemResult<IReadOnlyList<string>> EnumerateFiles(string directoryPath, string searchPattern = "*", bool recursive = false) =>
        FileSystemResult<IReadOnlyList<string>>.Failure(OperationStatus.Unsupported, "Remote Windows filesystem access is not implemented.");

    public FileSystemResult<IReadOnlyList<string>> EnumerateDirectories(string directoryPath, string searchPattern = "*", bool recursive = false) =>
        FileSystemResult<IReadOnlyList<string>>.Failure(OperationStatus.Unsupported, "Remote Windows filesystem access is not implemented.");

    public FileSystemResult<string> ReadLinkTarget(string path) => FileSystemResult<string>.Failure(OperationStatus.Unsupported, "Remote Windows filesystem access is not implemented.");
}
