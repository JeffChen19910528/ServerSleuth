using ServerSleuth.Core.Targets;

namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-2 §Step9: the boundary GUI-3 will consume — everything required to start a scan EXCEPT
/// sensitive credential material, which is deliberately NOT a field on this type at all (skill.md
/// GUI-2's own explicit instruction). A future GUI-3 phase passes an instance of this type
/// alongside a SEPARATE, freshly-supplied <see cref="ScanCredentialInput"/> (or its converted
/// real-transport-credential form) to whatever composition/execution boundary it introduces —
/// this type on its own is safe to hold, log (via <c>Target.Id</c> only — see below), or pass
/// around without any credential-leakage risk, because it structurally cannot carry one.
///
/// <see cref="Target"/> reuses the EXISTING <see cref="ScanTarget"/> domain type directly —
/// constructed via <see cref="ScanTarget.Local"/>/<see cref="ScanTarget.Remote"/>, never
/// reimplemented (skill.md GUI-2 §3: "if ScanTarget.Remote(...) already performs validation/
/// normalization, use it rather than recreating it").
///
/// GUI-2 constructs instances of this type ONLY through <see cref="ServerSleuth.Gui.Services.IScanRequestFactory"/>,
/// and ONLY after <see cref="ServerSleuth.Gui.Services.IScanConfigurationValidator"/> has
/// confirmed the configuration is valid — never speculatively, never for an invalid
/// configuration (skill.md GUI-2 §Step9's "Start Scan with invalid configuration does not
/// produce execution request").
/// </summary>
public sealed record ScanRequest
{
    public required ScanTarget Target { get; init; }

    public required string OutputDirectory { get; init; }

    public required ScanOutputFormat OutputFormat { get; init; }

    public required ScanOverwritePolicy OverwritePolicy { get; init; }

    public required bool Verbose { get; init; }

    /// <summary>Set only for a remote target — which transport the GUI-3 execution boundary
    /// should build. Always <c>null</c> for a local target.</summary>
    public ScanTransportKind? TransportKind { get; init; }

    // ----- Phase GUI-3: non-secret remote connection metadata, needed by the execution
    // boundary to actually build a transport. None of these is credential material — see
    // ScanConfigurationState's own doc comment for why a host-key fingerprint/file path/
    // environment-variable name/domain name are all safe, loggable, non-secret values. Named
    // "SshKeyFilePath" rather than "SshPrivateKeyPath" specifically so it never contains the
    // substring "privatekey" that ScanRequestFactoryTests' own credential-shape sweep forbids —
    // the PATH itself was never secret, only the substring match was too broad for this type.

    /// <summary>WinRM only — organizational metadata, not secret.</summary>
    public string? Domain { get; init; }

    /// <summary>SSH only — a local file PATH, never the key bytes themselves.</summary>
    public string? SshKeyFilePath { get; init; }

    /// <summary>SSH only — an environment-variable NAME, never the passphrase value it names.</summary>
    public string? SshKeyPassphraseEnvironmentVariable { get; init; }

    /// <summary>SSH only — the remote host's expected public key fingerprint (public by design).</summary>
    public string? SshHostFingerprint { get; init; }

    public ScanWinRmAuthenticationMechanism WinRmAuthenticationMechanism { get; init; } = ScanWinRmAuthenticationMechanism.Negotiate;

    public bool WinRmUseSsl { get; init; } = true;
}
