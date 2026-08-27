namespace ServerSleuth.Infrastructure.Security;

/// <summary>
/// Detects and redacts secret-shaped values before they can enter a DiscoveryEntity, Evidence
/// record, or log line. See skill.md §24: output must be "[REDACTED]" or a
/// "SecretDetected: true" flag, never the raw value.
/// </summary>
public interface ISecretRedactor
{
    /// <summary>Returns input with every detected secret value replaced by a redaction marker.</summary>
    string Redact(string input);

    /// <summary>True if any known secret pattern matches, without modifying the input.</summary>
    bool ContainsSecret(string input);
}
