namespace ServerSleuth.Cli.Output;

/// <summary>Fixed, deterministic help text — see skill.md (Phase 10A) §13: "Keep help
/// deterministic." Plain constants, never built from environment/machine-specific data.</summary>
internal static class HelpText
{
    public const string Root = """
        ServerSleuth — cross-platform server discovery and migration assessment tool.

        Usage:
          serversleuth --help
          serversleuth --version
          serversleuth scan [options]

        Commands:
          scan          Discover this server and produce a migration assessment report.

        Options:
          --help, -h    Show this help message.
          --version     Show the version number.

        Run 'serversleuth scan --help' for scan options.
        """;

    public const string Scan = """
        Usage: serversleuth scan [options]

        Discovers this server, runs the full migration assessment pipeline, and writes
        a report to the output directory.

        Options:
          --output <directory>    Output directory for the report. Default: ./serversleuth-report
          --format <json|html|both>
                                   Which report format(s) to write. Default: both
          --overwrite              Overwrite an existing report in the output directory.
                                    Default: off — an existing report is never silently overwritten.
          --quiet, -q               Suppress progress output; only errors are printed.
          --verbose                 Show per-scanner status/entity counts and stage durations.
                                    Ignored if --quiet is also given.
          --target <local|host>     Which machine to scan. 'local' (default) or a remote host
                                    name/IP — a Linux host requires every --ssh-* option below;
                                    a Windows host requires --winrm-user/--winrm-password-env.
          --ssh-user <name>         SSH username for a remote Linux target.
          --ssh-key <path>          Path to a private key file for a remote Linux target.
          --ssh-key-passphrase-env <VAR>
                                    Name of an environment variable holding the private key's
                                    passphrase, if it has one. Never pass a passphrase directly.
          --ssh-port <port>         SSH port for a remote Linux target. Default: 22.
          --ssh-host-fingerprint <sha256>
                                    The remote host's expected SSH key fingerprint. Required for
                                    a remote Linux target — unknown host keys are rejected by default.
          --winrm-user <name>       WinRM username for a remote Windows target.
          --winrm-password-env <VAR>
                                    Name of an environment variable holding the WinRM password.
                                    Never pass a password directly.
          --winrm-domain <domain>   Domain for the WinRM credential, if applicable.
          --winrm-port <port>       WinRM port for a remote Windows target. Default: 5986 (TLS)
                                    or 5985 (no TLS, with --winrm-no-ssl).
          --winrm-no-ssl            Use the non-TLS WinRM listener (port 5985 by default).
                                    Message-level Negotiate/Kerberos encryption still applies —
                                    never plaintext. Default: off (TLS required).
          --winrm-auth <negotiate|kerberos|credssp>
                                    WinRM authentication mechanism. Default: negotiate.
          --help, -h                Show this help message.
        """;
}
