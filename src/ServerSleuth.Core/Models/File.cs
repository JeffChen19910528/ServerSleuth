namespace ServerSleuth.Core.Models;

/// <summary>A discovered file relevant to migration (executable, native module, script, etc.).</summary>
public sealed class File : DiscoveryEntity
{
    public long? SizeBytes { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public string? Hash { get; init; }
}
