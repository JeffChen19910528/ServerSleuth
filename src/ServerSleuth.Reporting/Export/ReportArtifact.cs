using System.Text;

namespace ServerSleuth.Reporting.Export;

/// <summary>
/// An immutable, already-rendered report artifact ready to be written to disk — see skill.md
/// (Phase 9C) §3. Carries only what <see cref="IReportRenderer"/> already produced
/// (<see cref="ReportRenderResult"/>) plus the safe file name it will be written under; it never
/// carries a secret, raw configuration, or anything not already present in the rendered content.
/// </summary>
public sealed record ReportArtifact
{
    /// <summary>The exact file name this artifact will be written under — never a path, never
    /// derived from discovered entity data. Validated for path/traversal safety at export time
    /// regardless of how the artifact was constructed (skill.md §7).</summary>
    public required string FileName { get; init; }

    public required ReportFormat Format { get; init; }

    /// <summary>The exact rendered content — byte-for-byte what the renderer produced, never
    /// re-rendered or modified here.</summary>
    public required string Content { get; init; }

    public required Encoding Encoding { get; init; }

    /// <summary>The exact byte length <see cref="Content"/> encodes to under <see cref="Encoding"/>
    /// — computed once at artifact-construction time via <c>Encoding.GetByteCount</c>, never the
    /// (potentially different, for multi-byte UTF-8 text) character count.</summary>
    public required long ContentLength { get; init; }
}
