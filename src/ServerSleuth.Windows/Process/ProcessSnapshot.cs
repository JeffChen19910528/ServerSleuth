namespace ServerSleuth.Windows.Process;

/// <summary>
/// What System.Diagnostics.Process reliably exposes for a given process, captured as plain
/// data so mapping logic can be unit-tested without a real process handle.
/// </summary>
public sealed record ProcessSnapshot
{
    public required int Pid { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public bool StartTimeAccessDenied { get; init; }
}
