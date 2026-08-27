using ServerSleuth.Core.Targets;

namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-2 §Step8: the NON-SENSITIVE half of scan configuration — everything here is safe to hold
/// in <see cref="GuiApplicationState"/>, log, or serialize, because none of it is a credential.
/// Deliberately does NOT include a username or password field — see <see cref="ScanCredentialInput"/>
/// for the transient, sensitive counterpart this type is always paired with but never merged
/// into (merging them into one object would make it trivially easy for a future change to
/// accidentally serialize/log/persist the sensitive half along with the safe half).
///
/// <see cref="SshHostFingerprint"/>/<see cref="SshPrivateKeyPath"/>/
/// <see cref="SshPrivateKeyPassphraseEnvironmentVariable"/>/<see cref="Domain"/> are here, not in
/// <see cref="ScanCredentialInput"/>, because none of them IS secret material — a host-key
/// fingerprint is a public value (the whole point of fingerprint-based trust), a file PATH is
/// not the key bytes themselves, an environment-variable NAME is not the passphrase VALUE it
/// names, and a domain name is organizational metadata — exactly matching how the EXISTING CLI
/// (<c>--ssh-host-fingerprint</c>/<c>--ssh-key</c>/<c>--ssh-key-passphrase-env</c>/<c>--winrm-domain</c>)
/// already treats every one of these as an ordinary, loggable argument, never a secret one.
/// </summary>
public sealed record ScanConfigurationState
{
    public TargetKind TargetKind { get; init; } = TargetKind.Local;

    public TargetPlatform Platform { get; init; } = ResolveLocalPlatform();

    public string RemoteHost { get; init; } = string.Empty;

    public int? RemotePort { get; init; }

    public ScanTransportKind? TransportKind { get; init; }

    /// <summary>WinRM only — organizational metadata, not secret (see type doc comment).</summary>
    public string? Domain { get; init; }

    /// <summary>SSH only — a local file PATH, never the key bytes themselves.</summary>
    public string? SshPrivateKeyPath { get; init; }

    /// <summary>SSH only — an environment-variable NAME, never the passphrase value it names.</summary>
    public string? SshPrivateKeyPassphraseEnvironmentVariable { get; init; }

    /// <summary>SSH only — the remote host's expected public key fingerprint (public by design).</summary>
    public string? SshHostFingerprint { get; init; }

    public ScanWinRmAuthenticationMechanism WinRmAuthenticationMechanism { get; init; } = ScanWinRmAuthenticationMechanism.Negotiate;

    /// <summary>WinRM only — defaults to <c>true</c> (TLS required), mirroring
    /// <c>WinRmConnectionOptions.UseSsl</c>'s own fail-closed default exactly.</summary>
    public bool WinRmUseSsl { get; init; } = true;

    public string OutputDirectory { get; init; } = string.Empty;

    public ScanOutputFormat OutputFormat { get; init; } = ScanOutputFormat.Both;

    public ScanOverwritePolicy OverwritePolicy { get; init; } = ScanOverwritePolicy.FailIfExists;

    public bool Verbose { get; init; }

    public static ScanConfigurationState Initial { get; } = new();

    /// <summary>Resolved ONCE, from the current process's own runtime — never probed over a
    /// network, exactly matching <c>ScanTarget</c>'s own established convention (Phase 10C) for
    /// how a LOCAL target's platform is determined.</summary>
    private static TargetPlatform ResolveLocalPlatform() =>
        OperatingSystem.IsWindows() ? TargetPlatform.Windows :
        OperatingSystem.IsLinux() ? TargetPlatform.Linux :
        TargetPlatform.Unknown;
}
