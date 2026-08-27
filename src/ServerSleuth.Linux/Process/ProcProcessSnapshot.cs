namespace ServerSleuth.Linux.Process;

/// <summary>One process's already-gathered raw facts from `/proc/&lt;pid&gt;/*` — deliberately
/// all-optional beyond Pid, since a permission-denied or vanished-mid-scan process still
/// produces a valid (if sparse) snapshot rather than an exception. See skill.md (Phase 6A) §4.</summary>
public sealed record ProcProcessSnapshot
{
    public required int Pid { get; init; }
    public int? ParentPid { get; init; }
    public string? Name { get; init; }
    public string? State { get; init; }
    public string? CommandLine { get; init; }
    public string? ExecutablePath { get; init; }
    public string? Uid { get; init; }
    public bool AccessDenied { get; init; }

    /// <summary>True when /proc/&lt;pid&gt;/status was readable but had no parseable "Name:"
    /// field at all — a genuinely malformed entry, distinct from AccessDenied or a normal
    /// kernel-thread/zombie process (both of which still have a valid Name).</summary>
    public bool MalformedEntry { get; init; }
}
