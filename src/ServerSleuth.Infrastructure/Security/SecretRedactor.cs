using System.Text.RegularExpressions;

namespace ServerSleuth.Infrastructure.Security;

/// <summary>
/// Default ISecretRedactor. Rules are deliberately anchored to a "key" immediately followed
/// by a separator (":" or "=") and a value token — a bare word like "token" or "secret"
/// appearing in prose, or as part of a longer identifier (e.g. "TokenType", "SecretQuestion"),
/// does not match, keeping false positives low. See skill.md §24.
/// </summary>
public sealed class SecretRedactor : ISecretRedactor
{
    private const string RedactionMarker = "[REDACTED]";

    // A key/value pair's value: a quoted string, or a run of characters with no whitespace/;/,.
    private const string ValueGroup = /* lang=regex */ "(?<value>\"[^\"]*\"|'[^']*'|[^\\s;,]+)";

    private static readonly IReadOnlyList<SecretRedactionRule> DefaultRules =
    [
        KeyValueRule("Password", @"(?:Password|Pwd|UserPassword)", SecretSeverity.Critical),
        KeyValueRule("ConnectionString", "ConnectionString", SecretSeverity.Critical),
        KeyValueRule("ApiKey", "API[_-]?KEY", SecretSeverity.High),
        KeyValueRule("Token", "TOKEN", SecretSeverity.High),
        KeyValueRule("Secret", "SECRET", SecretSeverity.High),
        KeyValueRule("PrivateKeyValue", "PRIVATE[_-]?KEY", SecretSeverity.Critical),
        // Compound key names common in JSON/YAML app configuration (camelCase or snake_case) —
        // matched as one token so "clientSecret"/"client_secret" both match, unlike the bare
        // "SECRET"/"TOKEN" rules above which require the word to stand alone.
        KeyValueRule("ClientSecret", "CLIENT[_]?SECRET", SecretSeverity.Critical),
        KeyValueRule("AccessToken", "ACCESS[_]?TOKEN", SecretSeverity.High),
        KeyValueRule("RefreshToken", "REFRESH[_]?TOKEN", SecretSeverity.High),
        new SecretRedactionRule
        {
            Name = "PrivateKeyBlock",
            Severity = SecretSeverity.Critical,
            RedactValueGroupOnly = false,
            Pattern = new Regex(
                @"-----BEGIN\s+((RSA|EC|DSA|OPENSSH)\s+)?PRIVATE KEY-----[\s\S]+?-----END\s+((RSA|EC|DSA|OPENSSH)\s+)?PRIVATE KEY-----",
                RegexOptions.Compiled)
        },
        new SecretRedactionRule
        {
            Name = "JsonWebToken",
            Severity = SecretSeverity.High,
            RedactValueGroupOnly = false,
            Pattern = new Regex(@"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b", RegexOptions.Compiled)
        },
        new SecretRedactionRule
        {
            Name = "BearerToken",
            Severity = SecretSeverity.High,
            Pattern = new Regex(@"\bBearer\s+(?<value>[A-Za-z0-9\-_.=]+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        }
    ];

    private readonly IReadOnlyList<SecretRedactionRule> _rules;

    public SecretRedactor() : this(DefaultRules)
    {
    }

    public SecretRedactor(IReadOnlyList<SecretRedactionRule> rules)
    {
        _rules = rules;
    }

    public string Redact(string input)
    {
        var result = input;

        foreach (var rule in _rules)
        {
            result = rule.Pattern.Replace(result, match => BuildReplacement(match, rule));
        }

        return result;
    }

    public bool ContainsSecret(string input) => _rules.Any(rule => rule.Pattern.IsMatch(input));

    private static string BuildReplacement(Match match, SecretRedactionRule rule)
    {
        if (!rule.RedactValueGroupOnly || !match.Groups["value"].Success)
        {
            return RedactionMarker;
        }

        var valueGroup = match.Groups["value"];
        return string.Concat(
            match.Value.AsSpan(0, valueGroup.Index - match.Index),
            RedactionMarker,
            match.Value.AsSpan(valueGroup.Index - match.Index + valueGroup.Length));
    }

    private static SecretRedactionRule KeyValueRule(string name, string keyPattern, SecretSeverity severity) => new()
    {
        Name = name,
        Severity = severity,
        // The optional ["']? after the key handles a quoted JSON/YAML key (e.g. "password": "x")
        // where a closing quote sits between the key and the separator. The leading boundary is
        // deliberately NOT a plain `\b` — `\b` would reject the ubiquitous Linux .env/systemd
        // convention of an underscore-prefixed key (`DB_PASSWORD=...`), since `_` is a word
        // character with no boundary before "PASSWORD". `(?<![A-Za-z])` instead only rejects a
        // *letter* immediately before the key — so `MyPassword=...`-style camelCase run-ons are
        // still correctly left unmatched (unchanged from before), while `DB_PASSWORD=...` now
        // matches. The trailing `\b` is untouched, so prefix false positives like
        // `TokenType=...`/`SecretQuestion=...` remain correctly rejected. Found via a Phase 6E
        // test failure on a synthetic `.env` fixture, not assumed — see skill.md (Phase 6E) §9.
        Pattern = new Regex($@"(?<![A-Za-z])(?:{keyPattern})\b[""']?\s*[:=]\s*{ValueGroup}", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };
}
