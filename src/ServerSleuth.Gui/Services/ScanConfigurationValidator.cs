using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.Services;

/// <summary>
/// The only <see cref="IScanConfigurationValidator"/> implementation — see that interface's own
/// doc comment for the side-effect-free contract this class must uphold. Mirrors the EXISTING
/// CLI's own actually-supported combinations exactly (skill.md GUI-2 §4: "do not expose transport
/// combinations the existing backend does not support") rather than the broader theoretical
/// backend capability: SSH here is private-key authentication only (matching
/// <c>ScanOptionsParser</c>'s real <c>--ssh-user</c>/<c>--ssh-key</c>/<c>--ssh-host-fingerprint</c>
/// surface, which never wires up <c>RemoteCredential.ForPassword</c> at the CLI layer at all).
/// </summary>
public sealed class ScanConfigurationValidator : IScanConfigurationValidator
{
    public ScanConfigurationValidationResult Validate(ScanConfigurationState configuration, ScanCredentialInput credentials)
    {
        var errors = new List<ScanConfigurationValidationError>();

        ValidateTarget(configuration, errors);
        ValidateTransportAndCredentials(configuration, credentials, errors);
        ValidateOutput(configuration, errors);

        return errors.Count == 0 ? ScanConfigurationValidationResult.Valid : ScanConfigurationValidationResult.Invalid(errors);
    }

    private static void ValidateTarget(ScanConfigurationState configuration, List<ScanConfigurationValidationError> errors)
    {
        if (configuration.TargetKind == TargetKind.Local)
        {
            if (configuration.Platform == TargetPlatform.Unknown)
            {
                errors.Add(new ScanConfigurationValidationError
                {
                    Field = nameof(ScanConfigurationState.Platform),
                    Message = "The current machine's platform could not be determined — local scanning requires Windows or Linux."
                });
            }

            return;
        }

        // Remote.
        if (string.IsNullOrWhiteSpace(configuration.RemoteHost))
        {
            errors.Add(new ScanConfigurationValidationError
            {
                Field = nameof(ScanConfigurationState.RemoteHost),
                Message = "A remote target requires a non-empty host name."
            });
        }

        if (configuration.Platform is not (TargetPlatform.Windows or TargetPlatform.Linux))
        {
            errors.Add(new ScanConfigurationValidationError
            {
                Field = nameof(ScanConfigurationState.Platform),
                Message = "A remote target requires an explicit platform — Windows or Linux."
            });
        }
    }

    private static void ValidateTransportAndCredentials(
        ScanConfigurationState configuration, ScanCredentialInput credentials, List<ScanConfigurationValidationError> errors)
    {
        if (configuration.TargetKind == TargetKind.Local)
        {
            return; // transport/credentials are meaningless for a local target — never validated, never required.
        }

        switch (configuration.Platform)
        {
            case TargetPlatform.Linux:
                if (configuration.TransportKind != ScanTransportKind.Ssh)
                {
                    errors.Add(new ScanConfigurationValidationError
                    {
                        Field = nameof(ScanConfigurationState.TransportKind),
                        Message = "A remote Linux target must use SSH — no other transport is supported."
                    });
                }

                ValidateSshCredentials(configuration, credentials, errors);
                break;

            case TargetPlatform.Windows:
                if (configuration.TransportKind != ScanTransportKind.WinRm)
                {
                    errors.Add(new ScanConfigurationValidationError
                    {
                        Field = nameof(ScanConfigurationState.TransportKind),
                        Message = "A remote Windows target must use WinRM — no other transport is supported."
                    });
                }

                ValidateWinRmCredentials(credentials, errors);
                break;
        }
    }

    private static void ValidateSshCredentials(
        ScanConfigurationState configuration, ScanCredentialInput credentials, List<ScanConfigurationValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(credentials.Username))
        {
            errors.Add(new ScanConfigurationValidationError { Field = nameof(ScanCredentialInput.Username), Message = "SSH requires a username." });
        }

        if (string.IsNullOrWhiteSpace(configuration.SshPrivateKeyPath))
        {
            errors.Add(new ScanConfigurationValidationError
            {
                Field = nameof(ScanConfigurationState.SshPrivateKeyPath),
                Message = "SSH requires a private key file path — password authentication is not offered by this application."
            });
        }

        if (string.IsNullOrWhiteSpace(configuration.SshHostFingerprint))
        {
            errors.Add(new ScanConfigurationValidationError
            {
                Field = nameof(ScanConfigurationState.SshHostFingerprint),
                Message = "SSH requires the remote host's expected key fingerprint — unknown host keys are never trusted automatically."
            });
        }
    }

    private static void ValidateWinRmCredentials(ScanCredentialInput credentials, List<ScanConfigurationValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(credentials.Username))
        {
            errors.Add(new ScanConfigurationValidationError { Field = nameof(ScanCredentialInput.Username), Message = "WinRM requires a username." });
        }

        if ((credentials.Password?.Length ?? 0) == 0)
        {
            errors.Add(new ScanConfigurationValidationError { Field = nameof(ScanCredentialInput.Password), Message = "WinRM requires a password." });
        }
    }

    private static void ValidateOutput(ScanConfigurationState configuration, List<ScanConfigurationValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(configuration.OutputDirectory))
        {
            errors.Add(new ScanConfigurationValidationError
            {
                Field = nameof(ScanConfigurationState.OutputDirectory),
                Message = "An output directory is required."
            });

            return;
        }

        // Shape-only check — never touches the filesystem (no directory creation, no existence
        // check, no write): just whether the string COULD be a valid path at all.
        if (configuration.OutputDirectory.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0)
        {
            errors.Add(new ScanConfigurationValidationError
            {
                Field = nameof(ScanConfigurationState.OutputDirectory),
                Message = "The output directory contains characters that are not valid in a file path."
            });
        }
    }
}
