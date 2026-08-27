using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Cli.Options;
using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.DependencyInjection;
using ServerSleuth.Infrastructure.Remote;
using ServerSleuth.Infrastructure.Targets;
using ServerSleuth.Linux;
#if SERVERSLEUTH_WINDOWS
using ServerSleuth.Windows.DependencyInjection;
using ServerSleuth.Windows.Remote;
#endif

namespace ServerSleuth.Cli.Composition;

/// <summary>
/// The CLI's own composition root — see skill.md (Phase 10A) §4, (Phase 10D-2) §20. Platform/
/// target selection happens HERE, once, at process startup — never inside
/// <c>DiscoveryEngine</c> (which stays a plain <c>foreach (var scanner in registry.Scanners)</c>
/// loop, agnostic to which OS/target registered them).
///
/// <c>ServerSleuth.Cli</c> is multi-targeted (<c>net8.0-windows</c>/<c>net8.0</c> — see its own
/// <c>.csproj</c> comment) because NuGet refuses to restore a plain <c>net8.0</c> project
/// referencing the <c>net8.0-windows</c>-only <c>ServerSleuth.Windows</c> project at all. The
/// <c>SERVERSLEUTH_WINDOWS</c> compilation symbol (defined only for the <c>net8.0-windows</c>
/// build) is what makes <see cref="Build"/> even capable of compiling a call to
/// <c>AddServerSleuthWindows()</c> — but WHETHER that call actually runs is still decided purely
/// at runtime, by <see cref="OperatingSystem.IsWindows"/>, exactly like the Linux branch below.
///
/// Phase 10D-2: when <see cref="ScanOptions.Remote"/> is set, scanner-registry selection switches
/// from "the host OS this process happens to run on" to "the TARGET's platform" — a remote scan
/// must never accidentally register (and thereby scan) the local machine's own Windows APIs
/// (skill.md §21-22). Only Linux/SSH is implemented; a remote target is always Linux by this
/// point (the CLI parser only ever builds <see cref="ScanOptions.Remote"/> for a non-'local'
/// <c>--target</c> value, and this type is the only place that value becomes a real
/// <see cref="TargetPlatform"/>).
/// </summary>
public static class CompositionRoot
{
    public static IServiceProvider Build(ScanOptions options)
    {
        var services = new ServiceCollection();

        // Some scanners (e.g. WindowsOsScanner) take an ILogger<T> constructor dependency —
        // AddLogging() registers the generic ILogger<T>/ILoggerFactory infrastructure needed to
        // satisfy that, exactly as the existing CrossPlatformCompositionTests composition root
        // already does.
        services.AddLogging();

        if (options.WindowsRemote is not null)
        {
#if SERVERSLEUTH_WINDOWS
            var windowsRemoteTransport = BuildWindowsRemoteTransport(options.WindowsRemote);
            services.AddServerSleuthInfrastructure(windowsRemoteTransport);
            services.AddServerSleuthWindowsRemote(windowsRemoteTransport.ProviderSet);
#else
            // Phase 10D-3B §2: ServerSleuth.Windows (and therefore the entire WinRM capability
            // model) is only referenced by the net8.0-windows build — the SAME pre-existing
            // compile-time constraint Phase 10A's own multi-targeting comment documents. A
            // Windows/WinRM remote scan can only be INITIATED from a Windows-hosted
            // serversleuth binary, never from the plain net8.0 (Linux-hosted) one.
            throw new NotSupportedException(
                "Windows/WinRM remote scanning requires the Windows build of ServerSleuth.Cli " +
                "(net8.0-windows) — it is not available when running on this platform.");
#endif
        }
        else
        {
            var remoteTransport = options.Remote is null ? null : BuildSshTransport(options.Remote);
            services.AddServerSleuthInfrastructure(remoteTransport);

            if (remoteTransport is null)
            {
#if SERVERSLEUTH_WINDOWS
                if (OperatingSystem.IsWindows())
                {
                    services.AddServerSleuthWindows();
                }
#endif

                if (OperatingSystem.IsLinux())
                {
                    services.AddServerSleuthLinux();
                }
            }
            else
            {
                // Phase 10D-2 §20-21: target platform drives registration for a remote scan, not
                // the host OS — this branch runs identically whether ServerSleuth itself is
                // running on Windows or Linux. A Linux/SSH remote target is always Linux here
                // (enforced by RemoteTargetTransportFactory.CreateSsh itself).
                services.AddServerSleuthLinux();
            }
        }

        // Registered last, deliberately — it must run AFTER whichever platform registration
        // above has already added its own IDiscoveryScanner implementations, so the registry
        // actually has scanners to enumerate (see AddServerSleuthDiscoveryEngine's own doc
        // comment in ServerSleuth.Infrastructure).
        services.AddServerSleuthDiscoveryEngine();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Reads the private key file and (if named) the passphrase environment variable ONCE, here,
    /// from exactly the sources the user explicitly named on the command line — never discovered,
    /// never guessed (skill.md §28: "Do not guess credentials. Do not discover credentials.").
    /// Constructs (but does not connect) the transport; <see cref="ScanCommand"/> connects it
    /// explicitly, right before discovery begins (skill.md §6).
    /// </summary>
    private static SshRemoteTargetTransport BuildSshTransport(RemoteScanOptions remote)
    {
        var privateKeyBytes = File.ReadAllBytes(remote.PrivateKeyPath);
        var passphrase = remote.PrivateKeyPassphraseEnvironmentVariable is null
            ? null
            : Environment.GetEnvironmentVariable(remote.PrivateKeyPassphraseEnvironmentVariable);

        var credential = RemoteCredential.ForPrivateKey(remote.Username, privateKeyBytes, passphrase);
        var target = ScanTarget.Remote(remote.Host, TargetPlatform.Linux, remote.Port);

        var connectionOptions = new SshConnectionOptions
        {
            Host = remote.Host,
            Port = remote.Port,
            Target = target,
            CredentialProvider = new StaticRemoteCredentialProvider(credential),
            HostKeyVerifier = new TrustedFingerprintHostKeyVerifier(remote.Host, remote.Port, remote.HostFingerprint)
        };

        return RemoteTargetTransportFactory.CreateSsh(target, connectionOptions);
    }

#if SERVERSLEUTH_WINDOWS
    /// <summary>
    /// Reads the password from its named environment variable ONCE, here, from exactly the
    /// source the user explicitly named — never discovered, never guessed (skill.md Phase
    /// 10D-3B §5, mirroring <see cref="BuildSshTransport"/>'s own credential-reading discipline
    /// exactly). Constructs (but does not connect) the transport; <c>ScanCommand</c> connects it
    /// explicitly, right before discovery begins.
    /// </summary>
    private static ServerSleuth.Windows.Remote.WindowsRemoteTargetTransport BuildWindowsRemoteTransport(WindowsRemoteScanOptions remote)
    {
        var passwordValue = Environment.GetEnvironmentVariable(remote.PasswordEnvironmentVariable)
            ?? throw new CliArgumentException($"Environment variable '{remote.PasswordEnvironmentVariable}' named by '--winrm-password-env' is not set.");

        var securePassword = new System.Security.SecureString();
        foreach (var ch in passwordValue)
        {
            securePassword.AppendChar(ch);
        }
        securePassword.MakeReadOnly();

        var mechanism = remote.AuthenticationMechanism switch
        {
            "kerberos" => ServerSleuth.Windows.Remote.WindowsRemoteAuthenticationMechanism.Kerberos,
            "credssp" => ServerSleuth.Windows.Remote.WindowsRemoteAuthenticationMechanism.CredSsp,
            _ => ServerSleuth.Windows.Remote.WindowsRemoteAuthenticationMechanism.Negotiate
        };

        var credential = new ServerSleuth.Windows.Remote.WindowsRemoteCredential
        {
            Domain = remote.Domain,
            UserName = remote.Username,
            Password = securePassword,
            AuthenticationMechanism = mechanism
        };

        var target = ScanTarget.Remote(remote.Host, TargetPlatform.Windows, remote.Port);

        var connectionOptions = new ServerSleuth.Windows.Remote.WinRmConnectionOptions
        {
            Host = remote.Host,
            Port = remote.Port,
            UseSsl = remote.UseSsl
        };

        var credentialProvider = new ServerSleuth.Windows.Remote.StaticWindowsRemoteCredentialProvider(credential);
        var capabilities = ServerSleuth.Windows.Remote.WindowsRemoteCapabilityFactory.CreateWinRm(target, connectionOptions, credentialProvider);
        var providerSet = new ServerSleuth.Windows.Remote.WinRmWindowsProviderSet(capabilities);

        return new ServerSleuth.Windows.Remote.WindowsRemoteTargetTransport(target, providerSet);
    }
#endif
}
