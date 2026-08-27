using System.Security;
using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;
using ServerSleuth.Infrastructure.DependencyInjection;
using ServerSleuth.Infrastructure.Remote;
using ServerSleuth.Infrastructure.Targets;
using ServerSleuth.Linux;
using ServerSleuth.Windows.DependencyInjection;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Gui.ExecutionHost;

/// <summary>
/// GUI-3's own composition root — the WPF-analog of <c>ServerSleuth.Cli.Composition.CompositionRoot</c>.
/// Builds (but never connects — <see cref="GuiScanExecutor"/> connects, right before discovery
/// starts, exactly matching <c>ScanCommand</c>'s own discipline) whichever transport
/// <see cref="ScanRequest.Target"/>/<see cref="ScanRequest.TransportKind"/> calls for, then wires
/// the SAME <c>AddServerSleuthInfrastructure</c>/<c>AddServerSleuthWindows</c>/
/// <c>AddServerSleuthLinux</c>/<c>AddServerSleuthWindowsRemote</c>/<c>AddServerSleuthDiscoveryEngine</c>
/// extension methods the CLI composition root already uses — no second registration scheme, no
/// reimplemented scanner-selection logic.
///
/// Since <c>ServerSleuth.Gui</c> (and therefore this project, which only ever runs underneath
/// it) is Windows-only (single TFM, unlike the CLI's own net8.0-windows/net8.0 split — see this
/// project's own .csproj comment), <c>ServerSleuth.Windows</c> is referenced unconditionally —
/// no <c>SERVERSLEUTH_WINDOWS</c> compile-symbol branching is needed here.
///
/// Credential handling mirrors the CLI's own discipline (skill.md §28: "never guess/discover
/// credentials") with one deliberate improvement: a WinRM password arrives here as a
/// <see cref="SecureString"/> DIRECTLY from <see cref="ScanCredentialInput.Password"/> (the GUI's
/// <c>PasswordBox</c> already produced one) — never converted to/from a plain <see cref="string"/>
/// or an environment variable the way the CLI's own <c>--winrm-password-env</c> convention
/// requires, since the GUI has no shell-history/process-listing leak risk to defend against in
/// the first place (see ARCHITECTURE.md's GUI-2 addendum for that same reasoning already applied
/// to the UI layer).
/// </summary>
internal static class DefaultGuiScanComposition
{
    public static GuiScanComposition Build(ScanRequest request, ScanCredentialInput credentials)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        if (request.Target.Kind == TargetKind.Local)
        {
            services.AddServerSleuthInfrastructure();
            if (OperatingSystem.IsWindows())
            {
                services.AddServerSleuthWindows();
            }

            if (OperatingSystem.IsLinux())
            {
                services.AddServerSleuthLinux();
            }
        }
        else if (request.TransportKind == ScanTransportKind.WinRm)
        {
            var windowsTransport = BuildWindowsRemoteTransport(request, credentials);
            services.AddServerSleuthInfrastructure(windowsTransport);
            services.AddServerSleuthWindowsRemote(windowsTransport.ProviderSet);
        }
        else
        {
            var sshTransport = BuildSshTransport(request, credentials);
            services.AddServerSleuthInfrastructure(sshTransport);
            services.AddServerSleuthLinux();
        }

        services.AddServerSleuthDiscoveryEngine();

        var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<ITargetTransport>();
        return new GuiScanComposition { Transport = transport, Services = provider };
    }

    /// <summary>Reads the private key file and (if named) the passphrase environment variable
    /// ONCE, here, from exactly the sources the caller explicitly named — never discovered,
    /// never guessed, mirroring <c>ServerSleuth.Cli.Composition.CompositionRoot.BuildSshTransport</c>
    /// exactly.</summary>
    private static SshRemoteTargetTransport BuildSshTransport(ScanRequest request, ScanCredentialInput credentials)
    {
        var privateKeyPath = request.SshKeyFilePath
            ?? throw new InvalidOperationException("A validated remote-Linux scan request must carry an SSH key file path.");
        var privateKeyBytes = File.ReadAllBytes(privateKeyPath);
        var passphrase = request.SshKeyPassphraseEnvironmentVariable is null
            ? null
            : Environment.GetEnvironmentVariable(request.SshKeyPassphraseEnvironmentVariable);

        var credential = RemoteCredential.ForPrivateKey(credentials.Username ?? string.Empty, privateKeyBytes, passphrase);
        var target = ScanTarget.Remote(request.Target.Host!, TargetPlatform.Linux, request.Target.Port);

        var connectionOptions = new SshConnectionOptions
        {
            Host = target.Host!,
            Port = request.Target.Port ?? 22,
            Target = target,
            CredentialProvider = new StaticRemoteCredentialProvider(credential),
            HostKeyVerifier = new TrustedFingerprintHostKeyVerifier(target.Host!, request.Target.Port ?? 22, request.SshHostFingerprint ?? string.Empty)
        };

        return RemoteTargetTransportFactory.CreateSsh(target, connectionOptions);
    }

    /// <summary>Mirrors <c>ServerSleuth.Cli.Composition.CompositionRoot.BuildWindowsRemoteTransport</c>
    /// exactly, except the password is already a <see cref="SecureString"/> handed straight
    /// through — see this type's own doc comment for why that is a deliberate, safer departure
    /// from the CLI's env-var-only convention rather than an oversight.</summary>
    private static WindowsRemoteTargetTransport BuildWindowsRemoteTransport(ScanRequest request, ScanCredentialInput credentials)
    {
        var target = ScanTarget.Remote(request.Target.Host!, TargetPlatform.Windows, request.Target.Port);

        var mechanism = request.WinRmAuthenticationMechanism switch
        {
            ScanWinRmAuthenticationMechanism.Kerberos => WindowsRemoteAuthenticationMechanism.Kerberos,
            ScanWinRmAuthenticationMechanism.CredSsp => WindowsRemoteAuthenticationMechanism.CredSsp,
            _ => WindowsRemoteAuthenticationMechanism.Negotiate
        };

        var password = credentials.Password
            ?? throw new InvalidOperationException("A validated remote-Windows scan request must carry a WinRM password.");

        var credential = new WindowsRemoteCredential
        {
            Domain = request.Domain,
            UserName = credentials.Username ?? string.Empty,
            Password = password,
            AuthenticationMechanism = mechanism
        };

        var connectionOptions = new WinRmConnectionOptions
        {
            Host = target.Host!,
            Port = request.Target.Port,
            UseSsl = request.WinRmUseSsl
        };

        var credentialProvider = new StaticWindowsRemoteCredentialProvider(credential);
        var capabilities = WindowsRemoteCapabilityFactory.CreateWinRm(target, connectionOptions, credentialProvider);
        var providerSet = new WinRmWindowsProviderSet(capabilities);

        return new WindowsRemoteTargetTransport(target, providerSet);
    }
}
