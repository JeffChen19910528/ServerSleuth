using ServerSleuth.Core.Enums;

namespace ServerSleuth.Linux.Native;

/// <summary>Static, read-only ELF header/dynamic-section facts — never obtained by loading or
/// executing the binary. See skill.md (Phase 6F) §4-9, §15.</summary>
public sealed record ElfAnalysisResult
{
    public required ElfParseStatus Status { get; init; }
    public ElfClass Class { get; init; } = ElfClass.Unknown;
    public ElfEndian Endian { get; init; } = ElfEndian.Unknown;

    /// <summary>Raw `e_machine` value, e.g. "EM_X86_64" — recorded even when
    /// <see cref="Architecture"/> is <see cref="EntityArchitecture.Unknown"/>, so an
    /// unrecognized machine value is never silently discarded.</summary>
    public string? Machine { get; init; }

    public EntityArchitecture Architecture { get; init; } = EntityArchitecture.Unknown;

    /// <summary>DT_NEEDED entries, in file order, exactly as they appear in the dynamic
    /// section's string table — never deduplicated, since duplicate DT_NEEDED entries are
    /// themselves a fact worth preserving.</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>DT_RPATH, split on `:`, in order — never merged with RUNPATH, since the two
    /// have different search-order semantics for the real dynamic loader.</summary>
    public IReadOnlyList<string> RPath { get; init; } = [];

    /// <summary>DT_RUNPATH, split on `:`, in order.</summary>
    public IReadOnlyList<string> RunPath { get; init; } = [];

    /// <summary>Human-readable explanation for a non-<see cref="ElfParseStatus.Parsed"/>
    /// status — e.g. "big-endian ELF is not supported" or "program header offset exceeds file
    /// length". Never a stack trace or raw exception text.</summary>
    public string? Diagnostic { get; init; }
}
