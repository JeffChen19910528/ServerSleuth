using ServerSleuth.Infrastructure.Configuration;
using ServerSleuth.Windows.Common;
using ServerSleuth.Windows.Configuration;

namespace ServerSleuth.Windows.Binaries;

/// <summary>
/// One normalized binary observation, possibly discovered from multiple contributing roots
/// (e.g. the same DLL sitting under an IIS app AND referenced by a COM registration) —
/// deduplicated by normalized path before this row is built, never one row per discovery
/// source. See skill.md §29.
/// </summary>
public sealed record BinaryDiscoveryRow
{
    public required string Path { get; init; }
    public required string FileName { get; init; }
    public required string Extension { get; init; }
    public required BinaryFileStatus FileStatus { get; init; }
    public long? SizeBytes { get; init; }
    public DateTimeOffset? LastWriteTimeUtc { get; init; }
    public required IReadOnlyList<ScanRoot> ContributingRoots { get; init; }
    public PeAnalysisResult? PeAnalysis { get; init; }
    public FileVersionMetadata? VersionMetadata { get; init; }
}
