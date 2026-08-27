using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.Services;

/// <summary>
/// The only <see cref="IScanRequestFactory"/> implementation. Reuses
/// <see cref="ScanTarget.Local"/>/<see cref="ScanTarget.Remote"/> directly for target identity —
/// never reimplements their normalization (skill.md GUI-2 §3). Callers (see
/// <see cref="ServerSleuth.Gui.ViewModels.ScanConfigurationViewModel"/>) are responsible for
/// only calling <see cref="Create"/> after <see cref="IScanConfigurationValidator"/> has already
/// confirmed the configuration is valid — this factory does not re-validate, so it must never be
/// called speculatively for an unvalidated or known-invalid configuration.
/// </summary>
public sealed class ScanRequestFactory : IScanRequestFactory
{
    public ScanRequest Create(ScanConfigurationState configuration)
    {
        var target = configuration.TargetKind == TargetKind.Local
            ? ScanTarget.Local(configuration.Platform)
            : ScanTarget.Remote(configuration.RemoteHost, configuration.Platform, configuration.RemotePort);

        var isRemote = configuration.TargetKind == TargetKind.Remote;

        return new ScanRequest
        {
            Target = target,
            OutputDirectory = configuration.OutputDirectory,
            OutputFormat = configuration.OutputFormat,
            OverwritePolicy = configuration.OverwritePolicy,
            Verbose = configuration.Verbose,
            TransportKind = isRemote ? configuration.TransportKind : null,
            Domain = isRemote ? configuration.Domain : null,
            SshKeyFilePath = isRemote ? configuration.SshPrivateKeyPath : null,
            SshKeyPassphraseEnvironmentVariable = isRemote ? configuration.SshPrivateKeyPassphraseEnvironmentVariable : null,
            SshHostFingerprint = isRemote ? configuration.SshHostFingerprint : null,
            WinRmAuthenticationMechanism = configuration.WinRmAuthenticationMechanism,
            WinRmUseSsl = configuration.WinRmUseSsl
        };
    }
}
