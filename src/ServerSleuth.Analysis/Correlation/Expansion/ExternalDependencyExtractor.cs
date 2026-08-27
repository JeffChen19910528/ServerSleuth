using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Correlation.Expansion;

/// <summary>
/// Converts a Configuration entity's already-detected, already-normalized metadata (Endpoint/
/// Database/NetworkPath — Phase 4E-1) into <see cref="ExternalDependency"/> entities. Reads
/// only <see cref="Configuration.Metadata"/> — never raw file text, which Configuration never
/// carries in the first place (skill.md's no-raw-content rule). Produces no SMTP entries: no
/// existing scanner detects SMTP references (only URI-shaped and ADO.NET-shaped text is
/// analyzed), and Phase 5C's instructions explicitly forbid expanding into a new parser
/// architecture to add one — see skill.md (Phase 5C) §16, and Known Limitations.
/// </summary>
public static class ExternalDependencyExtractor
{
    public static IReadOnlyList<ExtractedDependency> Extract(Configuration configuration)
    {
        var results = new List<ExtractedDependency>();

        results.AddRange(ExtractEndpoints(configuration));
        results.AddRange(ExtractDatabases(configuration));
        results.AddRange(ExtractFileShares(configuration));

        return results;
    }

    private static IEnumerable<ExtractedDependency> ExtractEndpoints(Configuration configuration)
    {
        var index = 0;
        while (configuration.Metadata.TryGetValue($"Endpoint{index}.Scheme", out var scheme))
        {
            configuration.Metadata.TryGetValue($"Endpoint{index}.Host", out var host);
            configuration.Metadata.TryGetValue($"Endpoint{index}.Port", out var portText);
            configuration.Metadata.TryGetValue($"Endpoint{index}.Path", out var path);
            var port = int.TryParse(portText, out var parsedPort) ? parsedPort : (int?)null;

            if (host is not null)
            {
                var isLdap = scheme.StartsWith("ldap", StringComparison.OrdinalIgnoreCase);
                var kind = isLdap ? ExternalDependencyKinds.Ldap : ExternalDependencyKinds.ExternalApi;
                var idPrefix = isLdap ? "ldap" : "api";
                var id = ExternalDependencyIdentity.ForScopedHost(idPrefix, scheme, host, port);

                var detail = $"{scheme}://{host}{(port is not null ? $":{port}" : string.Empty)}{path ?? string.Empty}";
                var entity = BuildEntity(id, detail, kind);
                entity.SetMetadata("Scheme", scheme);
                entity.SetMetadata("Host", host);
                if (port is not null) entity.SetMetadata("Port", port.Value.ToString());
                if (path is not null) entity.SetMetadata("Path", path);

                yield return new ExtractedDependency { Entity = entity, ReferenceDetail = detail };
            }

            index++;
        }
    }

    private static IEnumerable<ExtractedDependency> ExtractDatabases(Configuration configuration)
    {
        var index = 0;
        while (configuration.Metadata.TryGetValue($"Database{index}.Type", out var type))
        {
            configuration.Metadata.TryGetValue($"Database{index}.Host", out var host);
            configuration.Metadata.TryGetValue($"Database{index}.Port", out var portText);
            configuration.Metadata.TryGetValue($"Database{index}.Name", out var databaseName);
            var port = int.TryParse(portText, out var parsedPort) ? parsedPort : (int?)null;

            if (host is not null)
            {
                var isRedis = string.Equals(type, "Redis", StringComparison.OrdinalIgnoreCase);
                var kind = isRedis ? ExternalDependencyKinds.Redis : ExternalDependencyKinds.Database;
                var id = isRedis
                    ? ExternalDependencyIdentity.ForHostPort("redis", host, port)
                    : ExternalDependencyIdentity.ForDatabase("database", type, host, port, databaseName);

                var displayName = $"{type} {host}{(port is not null ? $":{port}" : string.Empty)}{(databaseName is not null ? $"/{databaseName}" : string.Empty)}";
                var entity = BuildEntity(id, displayName, kind);
                entity.SetMetadata("DatabaseType", type);
                entity.SetMetadata("Host", host);
                if (port is not null) entity.SetMetadata("Port", port.Value.ToString());
                if (databaseName is not null) entity.SetMetadata("Database", databaseName);

                yield return new ExtractedDependency { Entity = entity, ReferenceDetail = displayName };
            }

            index++;
        }
    }

    private static IEnumerable<ExtractedDependency> ExtractFileShares(Configuration configuration)
    {
        var index = 0;
        while (configuration.Metadata.TryGetValue($"NetworkPath{index}.Server", out var server))
        {
            configuration.Metadata.TryGetValue($"NetworkPath{index}.Share", out var share);
            configuration.Metadata.TryGetValue($"NetworkPath{index}.Path", out var subPath);

            if (share is not null)
            {
                var id = ExternalDependencyIdentity.ForFileShare(server, share);
                var displayName = $@"\\{server}\{share}{(subPath is not null ? $"\\{subPath.TrimStart('\\')}" : string.Empty)}";

                var entity = BuildEntity(id, displayName, ExternalDependencyKinds.FileShare);
                entity.SetMetadata("Server", server);
                entity.SetMetadata("Share", share);
                if (subPath is not null) entity.SetMetadata("Path", subPath);

                yield return new ExtractedDependency { Entity = entity, ReferenceDetail = displayName };
            }

            index++;
        }
    }

    private static ExternalDependency BuildEntity(string id, string name, string kind)
    {
        var entity = new ExternalDependency
        {
            Id = id,
            Name = name,
            Type = "ExternalDependency",
            Source = "Configuration",
            Status = EntityStatus.Referenced,
            Confidence = Confidence.Medium(),
            Kind = kind,
            Endpoint = name
        };

        return entity;
    }
}
