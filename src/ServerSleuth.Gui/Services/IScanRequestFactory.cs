using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.Services;

/// <summary>GUI-2 §Step9: the boundary GUI-3 will consume. Deliberately takes only
/// <see cref="ScanConfigurationState"/> — never <see cref="ScanCredentialInput"/> — since the
/// produced <see cref="ScanRequest"/> structurally cannot and must not carry credential
/// material.</summary>
public interface IScanRequestFactory
{
    ScanRequest Create(ScanConfigurationState configuration);
}
