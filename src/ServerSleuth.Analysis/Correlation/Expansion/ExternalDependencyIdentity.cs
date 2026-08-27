namespace ServerSleuth.Analysis.Correlation.Expansion;

/// <summary>
/// Deterministic identity for <see cref="ServerSleuth.Core.Models.ExternalDependency"/> entities
/// — see skill.md (Phase 5C) §26. Case-folds host/type/share for the id (so the same dependency
/// observed with differing case across two configuration files still merges into one entity),
/// and never fabricates a missing port — an absent port is an absent id segment, not a guess.
/// </summary>
public static class ExternalDependencyIdentity
{
    public static string ForScopedHost(string prefix, string scheme, string host, int? port)
    {
        var parts = new List<string> { prefix, scheme.ToLowerInvariant(), host.ToLowerInvariant() };
        if (port is not null)
        {
            parts.Add(port.Value.ToString());
        }

        return string.Join(":", parts);
    }

    public static string ForDatabase(string prefix, string type, string host, int? port, string? databaseName)
    {
        var parts = new List<string> { prefix, type.ToLowerInvariant(), host.ToLowerInvariant() };
        if (port is not null)
        {
            parts.Add(port.Value.ToString());
        }

        if (databaseName is not null)
        {
            parts.Add(databaseName.ToLowerInvariant());
        }

        return string.Join(":", parts);
    }

    public static string ForHostPort(string prefix, string host, int? port)
    {
        var parts = new List<string> { prefix, host.ToLowerInvariant() };
        if (port is not null)
        {
            parts.Add(port.Value.ToString());
        }

        return string.Join(":", parts);
    }

    public static string ForFileShare(string server, string share) =>
        $@"fileshare:\\{server.ToLowerInvariant()}\{share.ToLowerInvariant()}";
}
