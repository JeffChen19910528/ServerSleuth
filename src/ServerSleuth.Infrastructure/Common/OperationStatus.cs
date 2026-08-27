namespace ServerSleuth.Infrastructure.Common;

/// <summary>
/// Outcome of a single infrastructure-level operation (process execution, file read, port
/// enumeration). Distinct from Core's ScannerStatus, which describes a whole scanner run —
/// this describes one primitive call within it. Callers translate this into a
/// DiscoveryError/ScannerStatus at the scanner layer; Infrastructure never throws for
/// expected failure modes. See skill.md §25-26.
///
/// <see cref="NotInstalled"/>/<see cref="TransportUnavailable"/>/<see cref="ProtocolError"/>
/// were added in Phase 10D-1 for the future remote transport's result model
/// (<c>RemoteOperationResult</c>) — reused here rather than a second, duplicate status enum,
/// per that phase's own §5 instruction. They are meaningful for local operations too
/// (<see cref="NotInstalled"/> already matches the existing scanner-level "tool not present"
/// case), so they live on this shared enum, not a remote-only one.
/// </summary>
public enum OperationStatus
{
    Success,
    AccessDenied,
    NotFound,
    Timeout,
    Cancelled,
    InvalidInput,
    IoError,
    ExecutionFailed,
    StartFailed,
    Unsupported,

    /// <summary>The queried tool/service does not exist on the target at all (e.g. no SSH
    /// server listening, no WinRM listener configured) — distinct from <see cref="NotFound"/>,
    /// which is a missing file/path, not a missing capability.</summary>
    NotInstalled,

    /// <summary>The transport itself (a future SSH/WinRM connection) could not be reached —
    /// distinct from any failure of the operation that transport would have carried.</summary>
    TransportUnavailable,

    /// <summary>The transport connected but the remote peer's response violated the expected
    /// protocol shape — distinct from <see cref="ExecutionFailed"/>, which is a clean failure
    /// response from a successfully executed operation.</summary>
    ProtocolError
}
