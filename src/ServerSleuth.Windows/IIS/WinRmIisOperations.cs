using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Windows.IIS;

/// <summary>
/// A DISCLOSED capability gap, not a hacked-around workaround (skill.md Phase 10D-3B §13, §34's
/// own explicit instruction: "if the selected WinRM implementation cannot safely support a
/// required capability, do not introduce PowerShell... document the exact capability gap...
/// classify as PartiallySupported... stop and report the limitation").
///
/// IIS exposes an optional WMI provider (<c>root\WebAdministration</c> — installed only when
/// the "IIS Management Scripts and Tools" feature is present), which WOULD be reachable through
/// the exact same <see cref="CimWinRmTransport"/> already used for <see cref="Wmi.IWindowsRemoteWmiOperations"/>.
/// This phase does NOT implement that mapping: the class/property schema could not be verified
/// against a real IIS host with the provider installed (no such host was available — skill.md
/// itself forbids fabricating or guessing at a schema this codebase cannot verify), and getting
/// it wrong would silently under- or mis-report IIS configuration, which is worse than reporting
/// nothing. Every call therefore returns <see cref="IisAvailability.NotInstalled"/> with a
/// diagnostic explaining exactly why — never fabricated site/binding data, and never a
/// PowerShell (<c>Get-IISSite</c>) fallback.
/// </summary>
public sealed class WinRmIisOperations(ScanTarget target) : IWindowsRemoteIisOperations
{
    public ScanTarget Target { get; } = target;

    public WindowsRemoteOperationResult<IisSnapshot> GetSnapshot() =>
        WindowsRemoteOperationResult<IisSnapshot>.Failure(
            OperationStatus.NotInstalled,
            "Remote IIS discovery is not implemented in Phase 10D-3B: no verified WS-Man-reachable structured " +
            "IIS schema (root\\WebAdministration) was available to validate against a real host. Disclosed gap, " +
            "not a PowerShell/appcmd workaround — see ARCHITECTURE.md's Phase 10D-3B addendum.");
}
