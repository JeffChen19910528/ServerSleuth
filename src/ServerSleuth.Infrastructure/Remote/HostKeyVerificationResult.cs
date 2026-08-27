namespace ServerSleuth.Infrastructure.Remote;

/// <summary>Outcome of an <see cref="IHostKeyVerifier"/> check — see skill.md (Phase 10D-2) §5.
/// There is no third "ask the user interactively" option: this codebase never blocks on
/// interactive input mid-scan, so an unrecognized host key is always <see cref="Rejected"/>,
/// never silently accepted.</summary>
public enum HostKeyVerificationResult
{
    Trusted,
    Rejected
}
