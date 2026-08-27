namespace ServerSleuth.Infrastructure.Remote;

/// <summary>
/// The default, fail-closed <see cref="IHostKeyVerifier"/> — see skill.md (Phase 10D-2) §5. Trusts
/// exactly the explicit set of "host:port → fingerprint" pairs it was constructed with; every
/// other host, port, or fingerprint mismatch is <see cref="HostKeyVerificationResult.Rejected"/>.
/// There is deliberately no "trust on first use" / "remember this host" behavior — an empty or
/// mismatched allow-list always rejects, never silently learns a new host key.
/// </summary>
public sealed class TrustedFingerprintHostKeyVerifier : IHostKeyVerifier
{
    private readonly IReadOnlyDictionary<string, string> _trustedFingerprintsByHostPort;

    public TrustedFingerprintHostKeyVerifier(IReadOnlyDictionary<string, string> trustedFingerprintsByHostPort)
    {
        _trustedFingerprintsByHostPort = trustedFingerprintsByHostPort;
    }

    /// <summary>Convenience constructor for the common single-host case.</summary>
    public TrustedFingerprintHostKeyVerifier(string host, int port, string trustedFingerprint)
        : this(new Dictionary<string, string> { [Key(host, port)] = trustedFingerprint })
    {
    }

    public HostKeyVerificationResult Verify(string host, int port, string fingerprint)
    {
        var key = Key(host, port);

        if (!_trustedFingerprintsByHostPort.TryGetValue(key, out var trusted))
        {
            return HostKeyVerificationResult.Rejected;
        }

        return string.Equals(trusted, fingerprint, StringComparison.OrdinalIgnoreCase)
            ? HostKeyVerificationResult.Trusted
            : HostKeyVerificationResult.Rejected;
    }

    private static string Key(string host, int port) => $"{host.Trim().ToLowerInvariant()}:{port}";
}
