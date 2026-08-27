namespace ServerSleuth.Gui.Models;

/// <summary>GUI-5 §Report Viewer: why an "Open Report" action did not succeed.</summary>
public enum GuiReportViewFailureReason
{
    None,
    NotFound,
    ReadFailed
}

/// <summary>
/// GUI-5 §2: the result of reading an ALREADY-GENERATED report file's raw text content for
/// display — never a re-render, never a second domain deserialization. <see cref="Content"/> is
/// the literal file bytes decoded as UTF-8 text; nothing here parses JSON into a second domain
/// model or interprets HTML markup — the viewer shows exactly what is already on disk.
/// </summary>
public sealed record GuiReportViewResult
{
    public required bool Success { get; init; }

    public string? Content { get; init; }

    public GuiReportViewFailureReason FailureReason { get; init; } = GuiReportViewFailureReason.None;

    /// <summary>A concise, user-safe message only — never a raw exception message/stack trace.</summary>
    public string? ErrorMessage { get; init; }

    public static GuiReportViewResult Succeeded(string content) => new() { Success = true, Content = content };

    public static GuiReportViewResult Failed(GuiReportViewFailureReason reason, string errorMessage) => new()
    {
        Success = false,
        FailureReason = reason,
        ErrorMessage = errorMessage
    };
}
