namespace ServerSleuth.Linux.Containers;

/// <summary>One published port mapping — see skill.md (Phase 6C) §12. Deliberately not
/// correlated to any host-listening Port entity here — that requires explicit namespace
/// evidence Phase 5 analysis may add later, not this scanner.</summary>
public sealed record PortMappingRow
{
    public string? HostAddress { get; init; }
    public int? HostPort { get; init; }
    public required int ContainerPort { get; init; }
    public required string Protocol { get; init; }
}
