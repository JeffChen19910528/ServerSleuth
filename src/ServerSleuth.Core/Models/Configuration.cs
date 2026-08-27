namespace ServerSleuth.Core.Models;

/// <summary>A discovered configuration file — see skill.md §44. Never carries full file
/// contents, only detected sections/references and a secret-presence flag.</summary>
public sealed class Configuration : DiscoveryEntity
{
    public string? Format { get; init; } // "xml", "json", "ini", etc.
    public IReadOnlyList<string> DetectedSections { get; init; } = [];
    public IReadOnlyList<string> DetectedDependencyReferences { get; init; } = [];
    public bool SecretDetected { get; init; }
}
