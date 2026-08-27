namespace ServerSleuth.Core.Models;

public sealed class ApplicationPool : DiscoveryEntity
{
    public string? ManagedRuntimeVersion { get; init; }
    public string? PipelineMode { get; init; }
    public string? Identity { get; init; }
    public string? StartMode { get; init; }
    public bool Enable32BitAppOnWin64 { get; init; }
}
