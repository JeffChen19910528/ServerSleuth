namespace ServerSleuth.Linux.Systemd;

public sealed record SystemdProbeResult
{
    public required SystemdAvailability Status { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<SystemdUnitRow> Units { get; init; } = [];
    public IReadOnlyList<string> PartialFailures { get; init; } = [];
}
