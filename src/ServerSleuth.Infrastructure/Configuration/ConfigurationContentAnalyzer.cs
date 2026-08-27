using System.Text.RegularExpressions;
using ServerSleuth.Infrastructure.Security;

namespace ServerSleuth.Infrastructure.Configuration;

/// <summary>
/// Universal, format-independent text scanning for secrets, external endpoints, database
/// references, network-share paths, Unix sockets, environment variable references, and runtime
/// references. Runs on already-decoded text regardless of format — a URL/UNC path/connection
/// string appears as a string value the same way whether the file is JSON, XML, or plain text,
/// so one shared implementation covers every format rather than duplicating this per parser or
/// per platform. Never returns raw secret values — everything passes through ISecretRedactor
/// first. See skill.md §8-18 (Windows), Phase 6E §9-15 (Linux). Moved here from
/// `ServerSleuth.Windows.Configuration` and extended in Phase 6E so Linux configuration
/// discovery can reuse it without a Linux→Windows dependency.
/// </summary>
public static class ConfigurationContentAnalyzer
{
    private static readonly Regex EndpointPattern = new(
        @"(?<scheme>https?|ldaps?)://(?<host>[A-Za-z0-9_.-]+)(?::(?<port>\d+))?(?<path>/[^\s""'<>]*)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UncPattern = new(
        @"\\\\(?<server>[A-Za-z0-9_.-]+)\\(?<share>[A-Za-z0-9_.$-]+)(?<path>\\[^\s""'<>|]*)?",
        RegexOptions.Compiled);

    /// <summary>Linux-style CIFS/SMB mount notation (`//server/share`) — distinct from
    /// <see cref="UncPattern"/>'s backslash Windows notation. The negative lookbehind for `:`
    /// keeps this from matching the `//` in `https://host/...`.</summary>
    private static readonly Regex CifsPattern = new(
        @"(?<!:)//(?<server>[A-Za-z0-9_.-]+)/(?<share>[A-Za-z0-9_.$-]+)(?<path>/[^\s""'<>]*)?",
        RegexOptions.Compiled);

    /// <summary>NFS export notation (`server:/export/path`). The negative lookahead for `//`
    /// keeps this from matching `scheme://host` URLs, and requiring the path to start with `/`
    /// keeps it from matching a bare `Key: value` prose line.</summary>
    private static readonly Regex NfsPattern = new(
        @"(?<server>[A-Za-z0-9][A-Za-z0-9_.-]*):(?!/{2})(?<path>/[A-Za-z0-9_./-]+)",
        RegexOptions.Compiled);

    private static readonly Regex UnixSocketPattern = new(
        @"(?:/run/|/var/run/|/tmp/)[\w.-]+(?:/[\w.-]+)*\.sock",
        RegexOptions.Compiled);

    private static readonly Regex EnvVarPercentPattern = new(@"%(?<name>[A-Za-z_][A-Za-z0-9_]*)%", RegexOptions.Compiled);

    /// <summary>Covers `${VAR}` and the shell default-value form `${VAR:-default}` in one
    /// pattern — added Phase 6E; the default's own text is never captured, only the variable
    /// name, since resolving/reading defaults is not this analyzer's job.</summary>
    private static readonly Regex EnvVarBracePattern = new(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?::-[^}]*)?\}", RegexOptions.Compiled);

    /// <summary>Bare `$VAR` (no braces) — the common shell/systemd/.env reference shape. Added
    /// Phase 6E. Never matches `${...}` forms since `{` isn't a valid identifier-start
    /// character, so this and <see cref="EnvVarBracePattern"/> never double-count the same
    /// reference.</summary>
    private static readonly Regex EnvVarBarePattern = new(@"\$(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    // Matched by shape, not by key adjacency — a connection string may be nested arbitrarily
    // deep (e.g. ASP.NET Core's "ConnectionStrings": { "Default": "Server=...;..." }), so
    // requiring the value to immediately follow a "connectionString" key would miss it, and
    // requiring surrounding quotes would miss INI-style unquoted "Key=Server=...;..." lines.
    // Matches from the first recognizable ADO.NET-style key up to end of line or a closing
    // quote, whichever comes first, working across JSON/XML/INI alike.
    private static readonly Regex ConnectionStringPattern = new(
        @"(?<value>(?:Server|Data Source|Initial Catalog|Host|Database|Uid)\s*=[^""\r\n]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Explicit target-framework-moniker detection, distinct from the family-only markers below
    // — anchored to a recognizable "TargetFramework" key (msbuild XML, JSON, or "Key=Value"
    // shapes alike) so a bare "net8.0"-looking substring elsewhere in a file is never picked up.
    private static readonly Regex TargetFrameworkPattern = new(
        @"TargetFrameworks?[""']?\s*(?:=|>|:)\s*[""']?(?<tfm>net\d+(?:\.\d+)?)[""']?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly (string Family, string[] Markers)[] RuntimeMarkers =
    [
        ("DotNet", ["DOTNET_ROOT", "dotnet.exe", "runtimeconfig.json"]),
        ("Java", ["JAVA_HOME", "java.exe", "javac.exe"]),
        ("Python", ["PYTHONHOME", "PYTHONPATH", "python.exe"]),
        ("Node", ["node_modules", "node.exe", "NODE_ENV"]),
        ("Php", ["php.ini", "php.exe"]),
        ("Go", ["GOROOT", "GOPATH", "go.exe"])
    ];

    public static ConfigurationAnalysisResult Analyze(string text, ISecretRedactor secretRedactor)
    {
        var envVars = EnvVarPercentPattern.Matches(text).Select(m => m.Groups["name"].Value)
            .Concat(EnvVarBracePattern.Matches(text).Select(m => m.Groups["name"].Value))
            .Concat(EnvVarBarePattern.Matches(text).Select(m => m.Groups["name"].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var runtimeReferences = RuntimeMarkers
            .Where(entry => entry.Markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Select(entry => entry.Family)
            .ToList();

        var runtimeVersionReferences = TargetFrameworkPattern.Matches(text)
            .Select(m => m.Groups["tfm"].Value.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ConfigurationAnalysisResult
        {
            SecretDetected = secretRedactor.ContainsSecret(text),
            ExternalEndpoints = ExtractEndpoints(text),
            NetworkPaths = ExtractUncPaths(text),
            NetworkStorageReferences = ExtractNetworkStorageReferences(text),
            UnixSocketReferences = ExtractUnixSockets(text),
            DatabaseReferences = ExtractDatabaseReferences(text),
            EnvironmentVariableReferences = envVars,
            RuntimeReferences = runtimeReferences,
            RuntimeVersionReferences = runtimeVersionReferences
        };
    }

    private static List<ExternalEndpointReference> ExtractEndpoints(string text) =>
        EndpointPattern.Matches(text)
            .Select(m => new ExternalEndpointReference
            {
                Scheme = m.Groups["scheme"].Value.ToLowerInvariant(),
                Host = m.Groups["host"].Value,
                Port = m.Groups["port"].Success ? int.Parse(m.Groups["port"].Value) : null,
                Path = m.Groups["path"].Success ? m.Groups["path"].Value : null
            })
            .GroupBy(e => (e.Scheme, e.Host, e.Port, e.Path))
            .Select(g => g.First())
            .ToList();

    private static List<UncPathReference> ExtractUncPaths(string text) =>
        UncPattern.Matches(text)
            .Select(m => new UncPathReference
            {
                Server = m.Groups["server"].Value,
                Share = m.Groups["share"].Value,
                Path = m.Groups["path"].Success ? m.Groups["path"].Value : null
            })
            .GroupBy(u => (u.Server, u.Share, u.Path))
            .Select(g => g.First())
            .ToList();

    private static List<NetworkStorageReference> ExtractNetworkStorageReferences(string text)
    {
        var results = new List<NetworkStorageReference>();

        results.AddRange(CifsPattern.Matches(text).Select(m => new NetworkStorageReference
        {
            Protocol = "CIFS",
            Server = m.Groups["server"].Value,
            Path = $"/{m.Groups["share"].Value}{m.Groups["path"].Value}"
        }));

        results.AddRange(NfsPattern.Matches(text).Select(m => new NetworkStorageReference
        {
            Protocol = "NFS",
            Server = m.Groups["server"].Value,
            Path = m.Groups["path"].Value
        }));

        return results
            .GroupBy(r => (r.Protocol, r.Server, r.Path))
            .Select(g => g.First())
            .ToList();
    }

    private static List<string> ExtractUnixSockets(string text) =>
        UnixSocketPattern.Matches(text).Select(m => m.Value).Distinct(StringComparer.Ordinal).ToList();

    private static List<DatabaseReference> ExtractDatabaseReferences(string text)
    {
        var results = new List<DatabaseReference>();

        foreach (Match match in ConnectionStringPattern.Matches(text))
        {
            var value = match.Groups["value"].Value;
            var pairs = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.Split('=', 2))
                .Where(kv => kv.Length == 2)
                .ToDictionary(kv => kv[0].Trim(), kv => kv[1].Trim(), StringComparer.OrdinalIgnoreCase);

            results.Add(Classify(pairs));
        }

        return results;
    }

    private static DatabaseReference Classify(Dictionary<string, string> pairs)
    {
        string? Get(params string[] keys) => keys.Select(k => pairs.GetValueOrDefault(k)).FirstOrDefault(v => v is not null);

        var host = Get("Server", "Data Source", "Host", "Addr");
        var portValue = Get("Port");
        int? port = int.TryParse(portValue, out var p) ? p : null;
        var database = Get("Initial Catalog", "Database", "Db");

        // MariaDB is wire-protocol-compatible with MySQL and cannot be distinguished from a
        // connection string alone — both classify as "MySql" here (documented known limitation).
        var type = pairs switch
        {
            _ when pairs.ContainsKey("Initial Catalog") || pairs.ContainsKey("Integrated Security") => "SqlServer",
            _ when host is not null && (host.EndsWith(".db") || host.EndsWith(".sqlite")) => "Sqlite",
            _ when pairs.ContainsKey("Uid") && pairs.ContainsKey("Database") => "MySql",
            _ when port == 5432 || pairs.ContainsKey("Username") => "PostgreSql",
            _ when port == 6379 => "Redis",
            _ => "Unknown"
        };

        return new DatabaseReference { Type = type, Host = host, Port = port, Database = database };
    }
}
