namespace ServerSleuth.Core.Models;

/// <summary>Locally installed/referenced database engine — see skill.md §45. Status
/// distinguishes Installed / Running / Listening / Referenced independently.</summary>
public sealed class Database : DiscoveryEntity
{
    public string? Engine { get; init; } // "SQL Server", "PostgreSQL", "Oracle", etc.
}
