using Microsoft.Extensions.Logging;

namespace ServerSleuth.Infrastructure.Common;

/// <summary>
/// Structured logging event names every scanner should emit — see skill.md §37.
/// Fixed as EventId constants so log messages are queryable/filterable rather than free text,
/// and so scanners never invent ad hoc event names.
/// </summary>
public static class ScannerLogEvents
{
    public static readonly EventId ScannerStarted = new(1000, nameof(ScannerStarted));
    public static readonly EventId ScannerCompleted = new(1001, nameof(ScannerCompleted));
    public static readonly EventId ScannerFailed = new(1002, nameof(ScannerFailed));
    public static readonly EventId ScannerSkipped = new(1003, nameof(ScannerSkipped));
    public static readonly EventId PermissionDenied = new(1004, nameof(PermissionDenied));
    public static readonly EventId DiscoveryCount = new(1005, nameof(DiscoveryCount));
}
