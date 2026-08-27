namespace ServerSleuth.Linux.Native;

/// <summary>One already-discovered binary path, its ELF analysis, and its resolved
/// dependencies — never carries raw binary content. See skill.md (Phase 6F) §16.</summary>
public sealed record NativeBinaryRow
{
    public required string Path { get; init; }
    public required NativeBinaryFileStatus FileStatus { get; init; }
    public long? SizeBytes { get; init; }
    public DateTimeOffset? LastWriteTimeUtc { get; init; }

    /// <summary>Every already-discovered entity (Process/Service/ScheduledTask/Runtime/Sdk) that
    /// referenced this exact path — never guessed, only what was actually observed.</summary>
    public IReadOnlyList<string> OwnerEntityIds { get; init; } = [];

    public ElfAnalysisResult? ElfAnalysis { get; init; }
    public IReadOnlyList<LibraryResolutionResult> ResolvedDependencies { get; init; } = [];
}
