namespace ServerSleuth.Infrastructure.Remote;

/// <summary>The SFTP-derived shape <see cref="SshFileSystemReader"/> maps onto
/// <see cref="FileSystem.FileEntryInfo"/>.</summary>
public sealed record SshRemoteFileInfo
{
    public required string FullPath { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset LastWriteTimeUtc { get; init; }
    public bool IsDirectory { get; init; }
    public bool IsSymbolicLink { get; init; }
}
