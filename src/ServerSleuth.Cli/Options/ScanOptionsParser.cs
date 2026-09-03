using ServerSleuth.Reporting;

namespace ServerSleuth.Cli.Options;

/// <summary>
/// Hand-rolled parser for <c>scan</c>'s own argument list — see skill.md (Phase 10A) §2: "Do not
/// introduce a large CLI framework unless skill.md explicitly requires one." skill.md's own CLI
/// section (§28) names only a small, fixed option surface, and Phase 10A's own §6 restricts it
/// further (<c>--output</c>/<c>--format</c>/<c>--overwrite</c>/<c>--quiet</c>) — a full parsing
/// library (e.g. System.CommandLine) would be a new package dependency for a job four
/// <c>switch</c> arms already handle deterministically.
/// </summary>
public static class ScanOptionsParser
{
    public static ScanOptions Parse(IReadOnlyList<string> args)
    {
        var outputDirectory = ScanOptions.DefaultOutputDirectory;
        var format = ReportFormatOption.Both;
        var overwrite = false;
        var quiet = false;
        var verbose = false;
        var language = ReportLanguage.ZhTw;  // default: Traditional Chinese
        string? targetValue = null;
        string? sshUser = null;
        string? sshKeyPath = null;
        string? sshKeyPassphraseEnv = null;
        var sshPort = 22;
        string? sshHostFingerprint = null;
        string? winRmUser = null;
        string? winRmPasswordEnv = null;
        string? winRmDomain = null;
        int? winRmPort = null;
        var winRmUseSsl = true;
        var winRmAuth = "negotiate";

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--output" or "-o":
                    outputDirectory = RequireValue(args, ref i, arg);
                    if (string.IsNullOrWhiteSpace(outputDirectory))
                    {
                        throw new CliArgumentException("--output requires a non-empty directory path.");
                    }
                    break;

                case "--format":
                    var formatValue = RequireValue(args, ref i, arg);
                    format = ParseFormat(formatValue);
                    break;

                case "--overwrite":
                    overwrite = true;
                    break;

                case "--quiet" or "-q":
                    quiet = true;
                    break;

                case "--verbose":
                    verbose = true;
                    break;

                case "--lang":
                    var langValue = RequireValue(args, ref i, arg);
                    language = ParseLanguage(langValue);
                    break;

                case "--target":
                    targetValue = RequireValue(args, ref i, arg);
                    break;

                case "--ssh-user":
                    sshUser = RequireValue(args, ref i, arg);
                    break;

                case "--ssh-key":
                    sshKeyPath = RequireValue(args, ref i, arg);
                    break;

                case "--ssh-key-passphrase-env":
                    sshKeyPassphraseEnv = RequireValue(args, ref i, arg);
                    break;

                case "--ssh-port":
                    var sshPortValue = RequireValue(args, ref i, arg);
                    if (!int.TryParse(sshPortValue, out sshPort) || sshPort is < 1 or > 65535)
                    {
                        throw new CliArgumentException($"'--ssh-port' must be a port number between 1 and 65535, got '{sshPortValue}'.");
                    }
                    break;

                case "--ssh-host-fingerprint":
                    sshHostFingerprint = RequireValue(args, ref i, arg);
                    break;

                case "--winrm-user":
                    winRmUser = RequireValue(args, ref i, arg);
                    break;

                case "--winrm-password-env":
                    winRmPasswordEnv = RequireValue(args, ref i, arg);
                    break;

                case "--winrm-domain":
                    winRmDomain = RequireValue(args, ref i, arg);
                    break;

                case "--winrm-port":
                    var winRmPortValue = RequireValue(args, ref i, arg);
                    if (!int.TryParse(winRmPortValue, out var parsedWinRmPort) || parsedWinRmPort is < 1 or > 65535)
                    {
                        throw new CliArgumentException($"'--winrm-port' must be a port number between 1 and 65535, got '{winRmPortValue}'.");
                    }
                    winRmPort = parsedWinRmPort;
                    break;

                case "--winrm-no-ssl":
                    winRmUseSsl = false;
                    break;

                case "--winrm-auth":
                    winRmAuth = RequireValue(args, ref i, arg);
                    if (winRmAuth is not ("negotiate" or "kerberos" or "credssp"))
                    {
                        throw new CliArgumentException($"'--winrm-auth' must be 'negotiate', 'kerberos', or 'credssp', got '{winRmAuth}'.");
                    }
                    break;

                default:
                    throw new CliArgumentException($"Unknown option '{arg}'. Run 'serversleuth scan --help' for usage.");
            }
        }

        if (targetValue is not null && !string.Equals(targetValue, "local", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(sshUser) && !string.IsNullOrWhiteSpace(winRmUser))
        {
            throw new CliArgumentException(
                $"Remote target '{targetValue}' cannot use both '--ssh-user' and '--winrm-user' — " +
                "a remote target is either a Linux/SSH host or a Windows/WinRM host, never both.");
        }

        var remote = string.IsNullOrWhiteSpace(winRmUser)
            ? ResolveRemoteOptions(targetValue, sshUser, sshKeyPath, sshKeyPassphraseEnv, sshPort, sshHostFingerprint)
            : null;

        var windowsRemote = string.IsNullOrWhiteSpace(winRmUser)
            ? null
            : ResolveWindowsRemoteOptions(targetValue, winRmUser, winRmPasswordEnv, winRmDomain, winRmPort, winRmUseSsl, winRmAuth);

        return new ScanOptions
        {
            OutputDirectory = outputDirectory,
            Format = format,
            Overwrite = overwrite,
            Quiet = quiet,
            Verbose = verbose,
            Language = language,
            Remote = remote,
            WindowsRemote = windowsRemote
        };
    }

    /// <summary>Phase 10D-3B §8: a Windows/WinRM remote requires <c>--winrm-user</c>/
    /// <c>--winrm-password-env</c>, rejected at parse time (before any I/O or network activity)
    /// if either is missing — never a silent fallback and never a password accepted as a direct
    /// argument.</summary>
    private static WindowsRemoteScanOptions? ResolveWindowsRemoteOptions(
        string? targetValue, string winRmUser, string? winRmPasswordEnv, string? winRmDomain, int? winRmPort, bool winRmUseSsl, string winRmAuth)
    {
        if (string.IsNullOrWhiteSpace(targetValue) || string.Equals(targetValue, "local", StringComparison.OrdinalIgnoreCase))
        {
            throw new CliArgumentException("'--winrm-user' requires a remote '--target <host>' — it has no meaning for a local scan.");
        }

        if (string.IsNullOrWhiteSpace(winRmPasswordEnv))
        {
            throw new CliArgumentException(
                $"Remote target '{targetValue}' requires '--winrm-password-env <VAR>' — a password is only ever " +
                "read from a named environment variable, never accepted as a direct argument.");
        }

        return new WindowsRemoteScanOptions
        {
            Host = targetValue,
            Port = winRmPort,
            UseSsl = winRmUseSsl,
            Domain = winRmDomain,
            Username = winRmUser,
            PasswordEnvironmentVariable = winRmPasswordEnv,
            AuthenticationMechanism = winRmAuth
        };
    }

    private static string RequireValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count)
        {
            throw new CliArgumentException($"'{optionName}' requires a value.");
        }

        index++;
        return args[index];
    }

    private static ReportFormatOption ParseFormat(string value) => value.ToLowerInvariant() switch
    {
        "json" => ReportFormatOption.Json,
        "html" => ReportFormatOption.Html,
        "both" => ReportFormatOption.Both,
        _ => throw new CliArgumentException($"Invalid format '{value}'. Expected 'json', 'html', or 'both'.")
    };

    private static ReportLanguage ParseLanguage(string value) => value.ToLowerInvariant() switch
    {
        "en" => ReportLanguage.En,
        "zh-tw" => ReportLanguage.ZhTw,
        "zh_tw" => ReportLanguage.ZhTw,
        _ => throw new CliArgumentException($"Invalid language '{value}'. Expected 'en' or 'zh-TW'.")
    };

    /// <summary>
    /// Phase 10C §11 established: <c>local</c>/omitted must behave identically, and an
    /// unsupported target must be rejected CLEARLY — never silently treated as local. Phase
    /// 10D-2 §6 fills in what a real remote value now means: any <c>--target</c> value other
    /// than <c>local</c> is a remote SSH host, and requires <c>--ssh-user</c>/<c>--ssh-key</c>/
    /// <c>--ssh-host-fingerprint</c> (skill.md §5: the default host-key verifier fails closed,
    /// so a fingerprint is never optional) — omitting any of them is rejected with a message
    /// naming exactly which option is missing, before any connection is ever attempted.
    /// Windows/WinRM targets remain entirely unsupported and are still rejected exactly like
    /// Phase 10C did for every remote target.
    /// </summary>
    private static RemoteScanOptions? ResolveRemoteOptions(
        string? targetValue, string? sshUser, string? sshKeyPath, string? sshKeyPassphraseEnv, int sshPort, string? sshHostFingerprint)
    {
        if (targetValue is null || string.Equals(targetValue, "local", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(targetValue))
        {
            throw new CliArgumentException("'--target' requires a non-empty host name (or 'local').");
        }

        if (string.IsNullOrWhiteSpace(sshUser))
        {
            throw new CliArgumentException($"Remote target '{targetValue}' requires '--ssh-user <username>'.");
        }

        if (string.IsNullOrWhiteSpace(sshKeyPath))
        {
            throw new CliArgumentException($"Remote target '{targetValue}' requires '--ssh-key <private-key-path>'.");
        }

        if (string.IsNullOrWhiteSpace(sshHostFingerprint))
        {
            throw new CliArgumentException(
                $"Remote target '{targetValue}' requires '--ssh-host-fingerprint <sha256-fingerprint>' — " +
                "unknown host keys are rejected by default and are never silently trusted.");
        }

        return new RemoteScanOptions
        {
            Host = targetValue,
            Port = sshPort,
            Username = sshUser,
            PrivateKeyPath = sshKeyPath,
            PrivateKeyPassphraseEnvironmentVariable = sshKeyPassphraseEnv,
            HostFingerprint = sshHostFingerprint
        };
    }
}
