namespace ServerSleuth.Linux.Systemd;

/// <summary>One systemd service unit's already-gathered raw facts, sourced from
/// `systemctl list-units --output=json` (unit/load/active/sub/description) merged with
/// `systemctl show &lt;unit&gt;` (everything else) — both machine-readable, never scraped from
/// prose output. See skill.md (Phase 6A) §7.</summary>
public sealed record SystemdUnitRow
{
    public required string UnitName { get; init; }
    public string? Description { get; init; }
    public string? LoadState { get; init; }
    public string? ActiveState { get; init; }
    public string? SubState { get; init; }
    public string? UnitFileState { get; init; }
    public string? ExecStart { get; init; }
    public string? User { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? FragmentPath { get; init; }
    public bool DetailUnavailable { get; init; }
}
