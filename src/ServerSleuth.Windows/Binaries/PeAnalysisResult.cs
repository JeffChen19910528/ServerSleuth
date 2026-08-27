using ServerSleuth.Core.Enums;

namespace ServerSleuth.Windows.Binaries;

/// <summary>Static, read-only PE header facts — never obtained by loading or executing the
/// binary. See skill.md §7-10.</summary>
public sealed record PeAnalysisResult
{
    public required PeParseStatus Status { get; init; }
    public BinaryType? BinaryType { get; init; }
    public bool IsManaged { get; init; }
    public string? Machine { get; init; }
    public EntityArchitecture Architecture { get; init; } = EntityArchitecture.Unknown;
    public bool Is64BitImage { get; init; }
    public string? Subsystem { get; init; }
    public long? ImageSizeBytes { get; init; }
    public DateTimeOffset? TimestampUtc { get; init; }
    public IReadOnlyList<string> Imports { get; init; } = [];
    public bool DelayImportsSupported { get; init; }
    public IReadOnlyList<string> DelayImports { get; init; } = [];
}
