namespace ServerSleuth.Core.Models;

/// <summary>Native/managed DLL dependency — see skill.md §16. Architecture mismatch against
/// its referencing application increases migration risk.</summary>
public sealed class Dll : DiscoveryEntity
{
    public IReadOnlyList<string> ReferencedByEntityIds { get; init; } = [];
    public IReadOnlyList<string> LoadedByEntityIds { get; init; } = [];
}
