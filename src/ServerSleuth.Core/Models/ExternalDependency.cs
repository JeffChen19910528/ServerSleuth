namespace ServerSleuth.Core.Models;

/// <summary>A reference to something outside this server (database, SMTP, HTTP/S API, LDAP,
/// AD, file share, UNC path, external service) — see skill.md §43. Discovered from static
/// configuration only; the tool never actively probes external systems.</summary>
public sealed class ExternalDependency : DiscoveryEntity
{
    public string? Kind { get; init; } // "Database", "SMTP", "HttpApi", "Ldap", "FileShare", etc.
    public string? Endpoint { get; init; }
}
