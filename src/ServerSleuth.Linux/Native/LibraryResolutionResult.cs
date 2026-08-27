namespace ServerSleuth.Linux.Native;

public sealed record LibraryResolutionResult
{
    public required string LibraryName { get; init; }
    public required LibraryResolutionStatus Status { get; init; }

    /// <summary>Only set when <see cref="Status"/> is <see cref="LibraryResolutionStatus.Resolved"/>.</summary>
    public string? ResolvedPath { get; init; }

    /// <summary>Which resolution tier produced <see cref="ResolvedPath"/> — "RPATH", "RUNPATH",
    /// "KnownBinary", "WellKnownLocation", or "Ldconfig" — see skill.md §10.</summary>
    public string? Source { get; init; }

    /// <summary>Only populated when <see cref="Status"/> is
    /// <see cref="LibraryResolutionStatus.Ambiguous"/> — every equally-valid candidate path,
    /// so the ambiguity is auditable rather than silently resolved to a guess.</summary>
    public IReadOnlyList<string> Candidates { get; init; } = [];
}
