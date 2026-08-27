using ServerSleuth.Core.Enums;

namespace ServerSleuth.Infrastructure.Runtimes;

/// <summary>
/// One normalized runtime/SDK observation produced by an IRuntimeDetector. Every distinct
/// version is its own row — never merged into "the newest one" (see skill.md §17). Fields
/// that could not be determined are null, never guessed or defaulted to empty string.
/// </summary>
public sealed record RuntimeDetectionRow
{
    public required string Family { get; init; } // "DotNetFramework","DotNetRuntime","DotNetSdk","Java","Python","Node","Npm","Php","Go"
    public required RuntimeEntityKind EntityKind { get; init; }
    public required string Name { get; init; } // display name, e.g. ".NET Framework", "Java (JDK)"
    public string? Version { get; init; }
    public string? Edition { get; init; } // vendor/distribution, e.g. "Eclipse Temurin" — never guessed from a directory name alone
    public EntityArchitecture Architecture { get; init; } = EntityArchitecture.Unknown;
    public string? InstallationPath { get; init; }
    public string? ExecutablePath { get; init; }
    public bool ExecutableAvailable { get; init; }

    public IReadOnlyList<string> DetectionSources { get; init; } = []; // "Registry","Command","KnownPath"
    public string? RegistryPath { get; init; }
    public string? Command { get; init; }
    public string? ConflictNote { get; init; } // e.g. "Registry reports 17, executable reports 21" — never silently resolved
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>(); // already-redacted values only
}
