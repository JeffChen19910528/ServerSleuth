namespace ServerSleuth.Infrastructure.Configuration;

/// <summary>Raw, normalized observation for one configuration file — never carries the file's
/// full raw content. See skill.md §6, §21.</summary>
public sealed record ConfigurationFileRow
{
    public required string Path { get; init; }
    public required string FileName { get; init; }
    public required ConfigurationFormat Format { get; init; }
    public required ConfigurationParseStatus ParseStatus { get; init; }
    public long? SizeBytes { get; init; }
    public DateTimeOffset? LastWriteTimeUtc { get; init; }
    public string? OwnerEntityId { get; init; }
    public required ScanRoot ScanRoot { get; init; }
    public ConfigurationAnalysisResult? Analysis { get; init; }
    public IReadOnlyList<string> DetectedSections { get; init; } = [];

    /// <summary>True when the filesystem entry itself is a symlink — the path actually opened
    /// (via <see cref="Path"/>) is still whatever the platform's own read call followed, but
    /// this flags the observation so a scanner can choose not to treat it as evidence of a
    /// second, independent file. Added Phase 6E (skill.md §28 — symlink boundary).</summary>
    public bool IsSymlink { get; init; }

    /// <summary>Technology-specific facts extracted by a per-technology parser (e.g. nginx
    /// `listen`/`server_name`, systemd `ExecStart`/`User`, sshd `Port`) — added Phase 6E.
    /// Always additional to, never a replacement for, <see cref="Analysis"/>'s generic
    /// cross-format scan.</summary>
    public IReadOnlyDictionary<string, string> TechnologyFacts { get; init; } = new Dictionary<string, string>();
}
