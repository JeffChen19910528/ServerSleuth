using System.Text.RegularExpressions;

namespace ServerSleuth.Linux.Configuration;

/// <summary>
/// Per-technology fact extraction for the file kinds named in skill.md (Phase 6E) §16-24 —
/// always in ADDITION to, never a replacement for, the generic cross-format
/// <c>ConfigurationContentAnalyzer</c> scan (secrets/endpoints/database references/etc. are
/// still detected for every file regardless of technology). Dispatch is driven by the file's
/// <see cref="ScanRoot.Source"/>, not by content sniffing — deliberately conservative
/// line/regex-based text analysis, never a full parser for nginx/Apache/systemd/postgresql
/// grammar, per skill.md's "conservative text analysis is acceptable" (§7).
/// </summary>
public static class LinuxConfigurationTechnologyAnalyzer
{
    public static IReadOnlyDictionary<string, string> Analyze(string source, string text) => source switch
    {
        "Systemd" or "ApplicationRoot" when LooksLikeSystemdUnit(text) => AnalyzeSystemdUnit(text),
        "Nginx" => AnalyzeNginx(text),
        "Apache" => AnalyzeApache(text),
        "Php" => AnalyzePhp(text),
        "MySql" => AnalyzeMySql(text),
        "PostgreSql" => AnalyzePostgres(text),
        "Ssh" => AnalyzeSsh(text),
        _ => new Dictionary<string, string>()
    };

    private static bool LooksLikeSystemdUnit(string text) =>
        text.Contains("[Unit]", StringComparison.Ordinal) || text.Contains("[Service]", StringComparison.Ordinal);

    // --- systemd unit (skill.md §16-17) ---

    private static readonly Regex SystemdKeyValue = new(@"^\s*(?<key>[A-Za-z]+)\s*=\s*(?<value>.+?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

    internal static Dictionary<string, string> AnalyzeSystemdUnit(string text)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match match in SystemdKeyValue.Matches(text))
        {
            var key = match.Groups["key"].Value;
            var value = match.Groups["value"].Value;

            switch (key)
            {
                case "Description":
                case "ExecStart":
                case "User":
                case "WorkingDirectory":
                case "Restart":
                case "After":
                case "Requires":
                case "Wants":
                    facts.TryAdd(key, value); // first occurrence wins — matches systemd's own override semantics closely enough for discovery purposes
                    break;
                case "EnvironmentFile":
                    // Multiple EnvironmentFile= lines are legal in systemd; index them so none are lost.
                    var index = 0;
                    while (facts.ContainsKey($"EnvironmentFile{index}"))
                    {
                        index++;
                    }
                    facts[$"EnvironmentFile{index}"] = value.TrimStart('-'); // leading "-" means "ignore if missing", not part of the path
                    break;
            }
        }

        return facts;
    }

    // --- nginx (skill.md §18) ---

    private static readonly Regex NginxDirective = new(
        @"^\s*(?<directive>listen|server_name|root|proxy_pass|include)\s+(?<value>[^;#]+);",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NginxUpstream = new(@"upstream\s+(?<name>[A-Za-z0-9_.-]+)\s*\{", RegexOptions.Compiled);

    internal static Dictionary<string, string> AnalyzeNginx(string text)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in NginxDirective.Matches(text))
        {
            var directive = match.Groups["directive"].Value.ToLowerInvariant();
            var value = match.Groups["value"].Value.Trim();
            var i = counters.GetValueOrDefault(directive);
            counters[directive] = i + 1;
            facts[$"{directive}{i}"] = value;
        }

        var upstreamNames = NginxUpstream.Matches(text).Select(m => m.Groups["name"].Value).Distinct().ToList();
        if (upstreamNames.Count > 0)
        {
            facts["upstreams"] = string.Join(",", upstreamNames);
        }

        return facts;
    }

    // --- Apache/httpd (skill.md §19) ---

    private static readonly Regex ApacheVirtualHost = new(@"<VirtualHost\s+(?<args>[^>]+)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ApacheDirective = new(
        @"^\s*(?<directive>Listen|ServerName|DocumentRoot|ProxyPass|Include)\s+(?<value>\S+)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static Dictionary<string, string> AnalyzeApache(string text)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var virtualHosts = ApacheVirtualHost.Matches(text).Select(m => m.Groups["args"].Value.Trim()).ToList();
        for (var i = 0; i < virtualHosts.Count; i++)
        {
            facts[$"VirtualHost{i}"] = virtualHosts[i];
        }

        foreach (Match match in ApacheDirective.Matches(text))
        {
            var directive = match.Groups["directive"].Value;
            var value = match.Groups["value"].Value;
            var i = counters.GetValueOrDefault(directive);
            counters[directive] = i + 1;
            facts[$"{directive}{i}"] = value;
        }

        return facts;
    }

    // --- PHP (skill.md §20) ---

    private static readonly Regex PhpExtension = new(@"^\s*(?:zend_)?extension\s*=\s*(?<name>\S+)", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static Dictionary<string, string> AnalyzePhp(string text)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        var extensions = PhpExtension.Matches(text).Select(m => m.Groups["name"].Value).Distinct().ToList();
        if (extensions.Count > 0)
        {
            facts["Extensions"] = string.Join(",", extensions);
        }

        return facts;
    }

    // --- MySQL/MariaDB (skill.md §21) ---

    private static readonly Regex MySqlDirective = new(
        @"^\s*(?<key>datadir|socket|port|bind-address)\s*=\s*(?<value>\S+)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static Dictionary<string, string> AnalyzeMySql(string text)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in MySqlDirective.Matches(text))
        {
            facts.TryAdd(match.Groups["key"].Value.ToLowerInvariant(), match.Groups["value"].Value);
        }

        return facts;
    }

    // --- PostgreSQL (skill.md §22) ---

    private static readonly Regex PostgresDirective = new(
        @"^\s*(?<key>port|listen_addresses|data_directory|include)\s*=\s*'?(?<value>[^'\r\n#]+?)'?\s*(?:#.*)?$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static Dictionary<string, string> AnalyzePostgres(string text)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in PostgresDirective.Matches(text))
        {
            facts.TryAdd(match.Groups["key"].Value.ToLowerInvariant(), match.Groups["value"].Value.Trim());
        }

        return facts;
    }

    // --- SSH (skill.md §24 — never read private key CONTENT, only the referenced path) ---

    private static readonly Regex SshDirective = new(
        @"^\s*(?<key>Port|ListenAddress|Include|HostKey)\s+(?<value>\S+)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static Dictionary<string, string> AnalyzeSsh(string text)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in SshDirective.Matches(text))
        {
            var key = match.Groups["key"].Value;
            var value = match.Groups["value"].Value;
            var i = counters.GetValueOrDefault(key);
            counters[key] = i + 1;
            facts[$"{key}{i}"] = value;
        }

        return facts;
    }
}
