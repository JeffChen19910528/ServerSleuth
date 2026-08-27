namespace ServerSleuth.Analysis.Correlation.Expansion;

/// <summary>Controlled vocabulary for <see cref="ServerSleuth.Core.Models.ExternalDependency.Kind"/>
/// values produced by Phase 5C — see skill.md (Phase 5C) §8.</summary>
public static class ExternalDependencyKinds
{
    public const string Database = "Database";
    public const string Redis = "Redis";
    public const string ExternalApi = "ExternalApi";
    public const string FileShare = "FileShare";
    public const string Ldap = "LDAP";
    public const string Smtp = "SMTP";
    public const string Unknown = "Unknown";
}
