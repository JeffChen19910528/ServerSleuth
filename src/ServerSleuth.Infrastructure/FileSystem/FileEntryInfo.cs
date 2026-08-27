namespace ServerSleuth.Infrastructure.FileSystem;

public sealed record FileEntryInfo
{
    public required string FullPath { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset LastWriteTimeUtc { get; init; }
    public bool IsDirectory { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsReparsePoint { get; init; }
}
