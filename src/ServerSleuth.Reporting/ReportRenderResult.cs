using System.Text;

namespace ServerSleuth.Reporting;

/// <summary>
/// The in-memory result of one <see cref="IReportRenderer.Render"/> call — see skill.md
/// (Phase 9A) §3, §12. Content lives entirely in memory as a CLR string; writing it to a file,
/// uploading it, or emailing it is explicitly out of scope for this phase (§12, §21) and belongs
/// to a later composition/CLI layer that decides what to do with this result.
/// </summary>
public sealed record ReportRenderResult
{
    public required ReportFormat Format { get; init; }

    /// <summary>The rendered report content. For <see cref="ReportFormat.Json"/> this is a
    /// complete, valid, UTF-8-safe JSON document (see skill.md §9).</summary>
    public required string Content { get; init; }

    /// <summary>The encoding a caller should use when persisting <see cref="Content"/> to bytes
    /// — always <see cref="Encoding.UTF8"/> today (§9). Exposed explicitly rather than assumed,
    /// since a later file-export phase needs to know it without re-deciding it.</summary>
    public required Encoding Encoding { get; init; }
}
