namespace ServerSleuth.Windows.Binaries;

public enum PeParseStatus
{
    Parsed,
    InvalidPe,
    Unreadable,
    NotAttempted // e.g. extension is known but file was skipped for another reason first
}
