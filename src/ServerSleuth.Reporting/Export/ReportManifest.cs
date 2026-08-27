namespace ServerSleuth.Reporting.Export;

/// <summary>
/// Optional, safe-metadata-only manifest describing a <see cref="ReportBundle"/>'s exported
/// artifacts — see skill.md (Phase 9C) §11-12. Contains only Format/FileName/ContentLength/a
/// SHA-256 hash of the exact bytes written — never a secret, credential, raw configuration
/// value, or environment variable. <see cref="CreatedAt"/> is opt-in and <c>null</c> unless the
/// caller explicitly supplies one (mirrors <c>HtmlReportRenderer</c>'s own <c>generatedAt</c>
/// pattern from Phase 9B) — a manifest with no creation timestamp is fully deterministic for the
/// same bundle.
/// </summary>
public sealed record ReportManifest
{
    public required IReadOnlyList<ReportManifestEntry> Artifacts { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>One artifact's entry in a <see cref="ReportManifest"/>.</summary>
public sealed record ReportManifestEntry
{
    public required string Format { get; init; }
    public required string FileName { get; init; }
    public required long ContentLength { get; init; }

    /// <summary>Lowercase hex SHA-256 of the exact bytes written to disk for this artifact —
    /// never used as a server/report identity (skill.md §12), purely an integrity check:
    /// <c>SHA256(read(file)) == Sha256</c> must hold for a correctly-exported artifact.</summary>
    public required string Sha256 { get; init; }
}
