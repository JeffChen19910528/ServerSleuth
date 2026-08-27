using System.Text.RegularExpressions;

namespace ServerSleuth.Infrastructure.Security;

/// <summary>
/// A single named, testable secret-detection rule. New credential shapes (JWT, cloud
/// credentials, Bearer tokens, ...) are added as new rules, never as ad hoc string checks
/// scattered through scanners — see skill.md §24, operating instructions §7.
/// </summary>
public sealed record SecretRedactionRule
{
    public required string Name { get; init; }
    public required Regex Pattern { get; init; }
    public required SecretSeverity Severity { get; init; }

    /// <summary>
    /// When true, only the regex's "value" capture group is replaced with the redaction
    /// marker (preserving the key name, e.g. "Password=[REDACTED]"). When false, the entire
    /// match is replaced (used for freestanding secrets like a PEM private key block or a JWT
    /// that has no surrounding key name).
    /// </summary>
    public bool RedactValueGroupOnly { get; init; } = true;
}
