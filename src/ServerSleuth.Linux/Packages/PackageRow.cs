namespace ServerSleuth.Linux.Packages;

/// <summary>One normalized package-manager entry — see skill.md (Phase 6B) §5. Fields that
/// could not be determined are null, never guessed.</summary>
public sealed record PackageRow
{
    public required string Name { get; init; }
    public string? Version { get; init; }
    public string? Architecture { get; init; }
    public string? Maintainer { get; init; }
    public string? Description { get; init; }
    public string? SourcePackage { get; init; }
    public string? InstallPath { get; init; }
}
