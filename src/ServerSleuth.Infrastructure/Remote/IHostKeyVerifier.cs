namespace ServerSleuth.Infrastructure.Remote;

/// <summary>
/// The host-key verification boundary — see skill.md (Phase 10D-2) §5. NEVER implement this as
/// "always trust" (no <c>AcceptAnyHostKey</c>/<c>TrustAllCertificates</c>/
/// <c>AutoAcceptUnknownHost</c> exists anywhere in this codebase, mechanically verified). An
/// unknown or mismatched host key must always resolve to
/// <see cref="HostKeyVerificationResult.Rejected"/>, producing a structured transport failure
/// (<see cref="Common.OperationStatus.TransportUnavailable"/>) rather than a silent connection.
/// Injectable so a caller can supply the specific set of hosts/fingerprints it has explicitly
/// decided to trust (skill.md §5: "If a host-key policy/provider is required, make it
/// injectable").
/// </summary>
public interface IHostKeyVerifier
{
    /// <summary><paramref name="fingerprint"/> is the connecting host's SHA-256 public-key
    /// fingerprint, already computed by the SSH session — this method never fetches or computes
    /// it itself, and never connects to anything on its own.</summary>
    HostKeyVerificationResult Verify(string host, int port, string fingerprint);
}
