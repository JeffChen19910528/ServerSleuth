namespace ServerSleuth.Gui.Models;

/// <summary>GUI-2 §Step7: one structured validation error, associated with the field that
/// caused it where useful (never a raw value — see <see cref="ScanConfigurationValidationResult"/>'s
/// own doc comment for the "never a credential value in a message" guarantee this enables).</summary>
public sealed record ScanConfigurationValidationError
{
    public required string Field { get; init; }

    public required string Message { get; init; }
}

/// <summary>
/// GUI-2 §Step7: the deterministic, side-effect-free result of validating a
/// <see cref="ScanConfigurationState"/>+<see cref="ScanCredentialInput"/> pair. Every
/// <see cref="ScanConfigurationValidationError.Message"/> is a FIXED, pre-written string chosen
/// by <see cref="ServerSleuth.Gui.Services.ScanConfigurationValidator"/> from a closed set — never
/// string-interpolated from a credential VALUE (a username/hostname is safe to interpolate and
/// may appear; a password/passphrase/private-key value never does, because the validator never
/// reads <see cref="ScanCredentialInput.Password"/>'s content at all — only whether it is
/// present, via <see cref="ScanCredentialInput.HasAnyValue"/>-style presence checks).
/// </summary>
public sealed record ScanConfigurationValidationResult
{
    public required bool IsValid { get; init; }

    public IReadOnlyList<ScanConfigurationValidationError> Errors { get; init; } = [];

    public static ScanConfigurationValidationResult Valid { get; } = new() { IsValid = true };

    public static ScanConfigurationValidationResult Invalid(IReadOnlyList<ScanConfigurationValidationError> errors) =>
        new() { IsValid = false, Errors = errors };
}
