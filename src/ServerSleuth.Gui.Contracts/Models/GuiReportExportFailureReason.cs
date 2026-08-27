namespace ServerSleuth.Gui.Models;

/// <summary>GUI-5 §Export State: why an "Export Report" action did not succeed — lets the
/// Results Dashboard show a specific, still-generic message (e.g. "a file already exists")
/// without ever exposing the underlying exception.</summary>
public enum GuiReportExportFailureReason
{
    None,
    AlreadyExists,
    InvalidPath,
    WriteFailed,
    Cancelled
}
