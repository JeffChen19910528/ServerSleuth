using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.Services;

/// <summary>
/// GUI-2 §Step7: validates a scan configuration deterministically, in memory, with NO side
/// effects — no DNS lookup, no ping, no TCP/SSH/WinRM connection attempt, no certificate
/// validation, no directory creation, no file write. <paramref name="credentials"/> is a
/// SEPARATE parameter from <paramref name="configuration"/> deliberately (see
/// <see cref="ScanConfigurationState"/>'s own doc comment) — an implementation may check
/// PRESENCE (<see cref="ScanCredentialInput.HasAnyValue"/>-shaped checks) but must never read or
/// echo a credential VALUE into a <see cref="ScanConfigurationValidationError.Message"/>.
/// </summary>
public interface IScanConfigurationValidator
{
    ScanConfigurationValidationResult Validate(ScanConfigurationState configuration, ScanCredentialInput credentials);
}
