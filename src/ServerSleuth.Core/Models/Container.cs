namespace ServerSleuth.Core.Models;

/// <summary>Docker container/image — see skill.md §19. Never carries env var values that
/// look like secrets, only names.</summary>
public sealed class Container : DiscoveryEntity
{
    public string? ImageTag { get; init; }
    public IReadOnlyList<string> Ports { get; init; } = [];
    public IReadOnlyList<string> Volumes { get; init; } = [];
    public IReadOnlyList<string> Networks { get; init; } = [];
    public IReadOnlyList<string> EnvironmentVariableNames { get; init; } = [];
    public string? Entrypoint { get; init; }
    public string? Command { get; init; }
    public string? RestartPolicy { get; init; }
}
