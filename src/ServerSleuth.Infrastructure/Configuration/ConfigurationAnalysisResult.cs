namespace ServerSleuth.Infrastructure.Configuration;

public sealed record ConfigurationAnalysisResult
{
    public bool SecretDetected { get; init; }
    public IReadOnlyList<string> DetectedSections { get; init; } = [];
    public IReadOnlyList<ExternalEndpointReference> ExternalEndpoints { get; init; } = [];
    public IReadOnlyList<DatabaseReference> DatabaseReferences { get; init; } = [];
    public IReadOnlyList<UncPathReference> NetworkPaths { get; init; } = [];

    /// <summary>NFS (`server:/export/path`) and CIFS/SMB (`//server/share`) references — added
    /// Phase 6E for Linux configuration discovery. Empty on Windows-authored configuration,
    /// since that syntax simply never matches there.</summary>
    public IReadOnlyList<NetworkStorageReference> NetworkStorageReferences { get; init; } = [];

    /// <summary>Explicit Unix domain socket paths (`/run/*.sock`, `/var/run/*.sock`,
    /// `/tmp/*.sock`) — added Phase 6E. Never connected to, never probed.</summary>
    public IReadOnlyList<string> UnixSocketReferences { get; init; } = [];

    public IReadOnlyList<string> EnvironmentVariableReferences { get; init; } = [];
    public IReadOnlyList<string> RuntimeReferences { get; init; } = [];

    /// <summary>Explicit target-framework-moniker-shaped versions found near a recognizable
    /// "TargetFramework" key (e.g. "net8.0") — distinct from <see cref="RuntimeReferences"/>,
    /// which only ever detects family-level presence markers with no version. Added for Phase
    /// 5C's Configuration→Runtime correlation, which must never guess a version when
    /// discovery only observed a bare family marker. See skill.md (Phase 5C) §18-19.</summary>
    public IReadOnlyList<string> RuntimeVersionReferences { get; init; } = [];
}
