using ServerSleuth.Reporting.Export;

namespace ServerSleuth.Cli.Options;

/// <summary>The parsed, validated options for <c>serversleuth scan</c> — see skill.md
/// (Phase 10A) §6. Deliberately only the options this phase actually asks for; no
/// <c>--profile</c>/<c>--verbose</c>/etc. invented ahead of need.</summary>
public sealed record ScanOptions
{
    public const string DefaultOutputDirectory = "./serversleuth-report";

    public string OutputDirectory { get; init; } = DefaultOutputDirectory;
    public ReportFormatOption Format { get; init; } = ReportFormatOption.Both;
    public bool Overwrite { get; init; }
    public bool Quiet { get; init; }

    /// <summary>Phase 10B §5-6, §13: when set, prints each scanner's real Id/Status/entity-count
    /// (read directly off <c>AggregateDiscoveryResult.ScannerResults</c> — never fabricated) and
    /// stage durations. Mutually exclusive with <see cref="Quiet"/> at the option level — parsed
    /// but not enforced there, since a caller passing both is simply choosing "quiet wins" (quiet
    /// suppresses ALL progress output, verbose only adds detail to progress output that quiet
    /// would already suppress).</summary>
    public bool Verbose { get; init; }

    public ReportOverwritePolicy OverwritePolicy => Overwrite ? ReportOverwritePolicy.Overwrite : ReportOverwritePolicy.FailIfExists;

    /// <summary>Phase 10D-2 §6, §20: non-null only when <c>--target</c> named a remote host
    /// (never "local"/omitted). Carries no secret itself — only the private-key file PATH and
    /// the environment-variable NAME to read a passphrase from, never key bytes/passphrase
    /// values (those are read once, at connection time, by the composition root — see
    /// skill.md's Credential Boundary).</summary>
    public RemoteScanOptions? Remote { get; init; }

    /// <summary>Phase 10D-3B §8, §20: non-null only when <c>--target</c> named a remote host AND
    /// <c>--winrm-user</c> was used (never both <see cref="Remote"/> and this — see
    /// <c>ScanOptionsParser</c>'s ambiguity check). Carries no secret itself — only the
    /// environment-variable NAME to read a password from, never the password value.</summary>
    public WindowsRemoteScanOptions? WindowsRemote { get; init; }
}

/// <summary>The minimum WinRM connection surface <c>scan --target &lt;host&gt; --winrm-user &lt;u&gt;</c>
/// accepts — see skill.md (Phase 10D-3B) §6-8. A password is read only from an environment
/// variable NAME, never as a direct argument (matching <see cref="RemoteScanOptions"/>'s own
/// passphrase-via-env-var convention) — so it can never appear in shell history or a process
/// listing.</summary>
public sealed record WindowsRemoteScanOptions
{
    public required string Host { get; init; }
    public int? Port { get; init; }
    public bool UseSsl { get; init; } = true;
    public string? Domain { get; init; }
    public required string Username { get; init; }
    public required string PasswordEnvironmentVariable { get; init; }
    public string AuthenticationMechanism { get; init; } = "negotiate";
}

/// <summary>The minimum SSH connection surface <c>scan --target &lt;host&gt;</c> accepts — see
/// skill.md (Phase 10D-2) §4-6. <see cref="HostFingerprint"/> is required, not optional: this
/// codebase's default host-key verifier (<c>TrustedFingerprintHostKeyVerifier</c>) fails closed,
/// so there is no way to reach a remote host without the caller explicitly stating which key
/// they trust.</summary>
public sealed record RemoteScanOptions
{
    public required string Host { get; init; }
    public int Port { get; init; } = 22;
    public required string Username { get; init; }
    public required string PrivateKeyPath { get; init; }
    public string? PrivateKeyPassphraseEnvironmentVariable { get; init; }
    public required string HostFingerprint { get; init; }
}
