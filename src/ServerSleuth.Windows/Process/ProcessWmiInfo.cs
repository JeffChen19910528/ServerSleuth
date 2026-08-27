namespace ServerSleuth.Windows.Process;

/// <summary>
/// The subset of Win32_Process (plus GetOwner()) used to augment a ProcessSnapshot.
/// Any field being null means it genuinely could not be obtained for this process
/// (permission, protected process, timing) — never an empty string standing in for unknown.
/// </summary>
public sealed record ProcessWmiInfo
{
    public required int ProcessId { get; init; }
    public string? ExecutablePath { get; init; }
    public string? CommandLine { get; init; }
    public int? ParentProcessId { get; init; }
    public string? OwnerDomain { get; init; }
    public string? OwnerUser { get; init; }
}
