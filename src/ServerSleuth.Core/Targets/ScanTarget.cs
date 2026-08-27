namespace ServerSleuth.Core.Targets;

/// <summary>
/// Identifies WHAT is being scanned, independent of HOW discovery reaches it — see skill.md
/// (Phase 10C) §2. A pure, immutable data record: no process/file/network handle, no
/// credential, no transport reference. <see cref="DiscoveryEntity"/>/<see cref="EvidenceRecord"/>/
/// risk/migration/report models never reference this type — target identity is a Discovery/CLI
/// concern, not a domain-entity concern (see skill.md §14, §17).
///
/// <see cref="Id"/> is always deterministic for the same logical target — never a random GUID
/// (skill.md §2, §15). The local target's Id is a fixed literal ("local"): there is exactly one
/// logical "the machine ServerSleuth is running on," so no hostname/GUID/timestamp is needed or
/// wanted. A future remote target's Id would be derived from its own stable identifying
/// information (e.g. a normalized hostname) — not implemented in this phase, since no remote
/// transport exists to give that Id any operational meaning yet.
///
/// Deliberately holds NO password/API-key/SSH-key/token/credential field of any kind, and never
/// will — see the Credential Boundary in ARCHITECTURE.md's Phase 10C addendum. A future
/// credential provider, if ever needed, is an entirely separate architectural concern from
/// target identity.
/// </summary>
public sealed record ScanTarget
{
    public const string LocalTargetId = "local";

    public required string Id { get; init; }
    public required TargetKind Kind { get; init; }
    public TargetPlatform Platform { get; init; } = TargetPlatform.Unknown;
    public string? DisplayName { get; init; }

    /// <summary>The normalized host name — set only for a <see cref="TargetKind.Remote"/>
    /// target. <see cref="Id"/> already embeds this (as <c>"remote:{host}"</c>) for identity/
    /// equality purposes, but a transport needs the raw value back out separately (skill.md
    /// Phase 10D-2 §6) rather than re-parsing <see cref="Id"/>.</summary>
    public string? Host { get; init; }

    /// <summary>The remote SSH/WinRM port — set only for a <see cref="TargetKind.Remote"/>
    /// target, defaulting to <c>null</c> (meaning "use the transport's own default port," e.g.
    /// 22 for SSH) rather than baking a protocol-specific default into the target model itself
    /// (skill.md §6).</summary>
    public int? Port { get; init; }

    /// <summary>The one local target — same fixed Id every time, so two local targets are
    /// always equal (skill.md §15: "two identical local targets must produce the same target
    /// identity"). <paramref name="platform"/> is supplied by the caller (resolved once, at the
    /// composition-root/CLI layer, from the current process's own OS) rather than probed here —
    /// this type never inspects the runtime environment itself.</summary>
    public static ScanTarget Local(TargetPlatform platform = TargetPlatform.Unknown, string? displayName = null) => new()
    {
        Id = LocalTargetId,
        Kind = TargetKind.Local,
        Platform = platform,
        DisplayName = displayName
    };

    /// <summary>
    /// Represents a remote target as data — constructing one carries no ability by itself to
    /// reach it; actually connecting requires a transport (Phase 10D-2 introduced the first one,
    /// SSH, for <see cref="TargetPlatform.Linux"/>). <paramref name="hostName"/> is validated
    /// (non-empty) and normalized (trimmed, lowercased) so the same logical remote host always
    /// produces the same <see cref="Id"/> — deterministic identity, still never a DNS
    /// resolution/connection/network probe of any kind (skill.md Phase 10D-1 §6, Phase 10D-2 §6).
    /// <paramref name="platform"/> defaults to <see cref="TargetPlatform.Unknown"/>, same as
    /// <see cref="Local"/> — nothing about a remote host's OS can be known without actually
    /// reaching it. <paramref name="port"/> defaults to <c>null</c> ("use the transport's own
    /// default").
    /// </summary>
    public static ScanTarget Remote(
        string hostName, TargetPlatform platform = TargetPlatform.Unknown, int? port = null, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(hostName))
        {
            throw new ArgumentException("A remote target requires a non-empty host name.", nameof(hostName));
        }

        var normalizedHost = hostName.Trim().ToLowerInvariant();

        return new ScanTarget
        {
            Id = $"remote:{normalizedHost}",
            Kind = TargetKind.Remote,
            Platform = platform,
            Host = normalizedHost,
            Port = port,
            DisplayName = displayName
        };
    }
}
