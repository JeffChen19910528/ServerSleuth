namespace ServerSleuth.Infrastructure.Configuration;

/// <summary>An explicit NFS (`server:/export/path`) or CIFS/SMB (`//server/share`) reference —
/// added Phase 6E for Linux configuration discovery (skill.md (Phase 6E) §15). Distinct from
/// <see cref="UncPathReference"/> (Windows `\\server\share` notation) since the wire syntax
/// differs even though the semantic shape is the same. Never accessed, never mounted, never
/// network-validated.</summary>
public sealed record NetworkStorageReference
{
    public required string Protocol { get; init; } // "NFS", "CIFS"
    public required string Server { get; init; }
    public required string Path { get; init; }
}
