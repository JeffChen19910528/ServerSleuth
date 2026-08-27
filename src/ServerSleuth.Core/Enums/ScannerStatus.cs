namespace ServerSleuth.Core.Enums;

/// <summary>
/// Outcome a scanner reports for its own run. A permission failure or missing
/// dependency must surface as one of these, never as an unhandled exception
/// that aborts the overall scan — see skill.md §25-26.
/// </summary>
public enum ScannerStatus
{
    Supported,
    PartiallySupported,
    AccessDenied,
    NotApplicable,
    NotInstalled,
    Failed
}
