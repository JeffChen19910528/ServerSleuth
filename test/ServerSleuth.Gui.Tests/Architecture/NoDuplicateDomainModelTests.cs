using ServerSleuth.Gui.Composition;

namespace ServerSleuth.Gui.Tests.Architecture;

/// <summary>
/// GUI-2 §Step13: no duplicate <c>ScanTarget</c>/<c>RemoteCredential</c>/<c>ReportFormatOption</c>/
/// <c>ReportOverwritePolicy</c> was created unnecessarily. <c>ServerSleuth.Gui</c> genuinely
/// reuses <c>ScanTarget</c> directly (it lives in the allowed <c>ServerSleuth.Core</c>
/// dependency — see <see cref="ScanRequestFactoryTests"/>). It does NOT reuse
/// <c>RemoteCredential</c>/<c>ReportFormatOption</c>/<c>ReportOverwritePolicy</c> directly,
/// because those live in <c>ServerSleuth.Infrastructure</c>/<c>ServerSleuth.Cli</c>/
/// <c>ServerSleuth.Reporting</c> — all outside GUI-1's own established dependency boundary,
/// which this phase does not relax (see <c>NoDirectPlatformAccessTests</c>). The GUI-local
/// mirrors (<c>ScanCredentialInput</c>/<c>ScanOutputFormat</c>/<c>ScanOverwritePolicy</c>) are
/// deliberately named DIFFERENTLY from the real types — this test proves no type sharing the
/// REAL type's exact name was ever created inside <c>ServerSleuth.Gui</c>, which would be a
/// genuine, confusing duplicate rather than a clearly-labeled presentation-layer mirror.
/// </summary>
public class NoDuplicateDomainModelTests
{
    private static readonly string[] ForbiddenExactTypeNames =
    [
        "ScanTarget", "RemoteCredential", "WindowsRemoteCredential", "ReportFormatOption", "ReportOverwritePolicy", "RemoteTransportKind"
    ];

    [Fact]
    public void GuiAssembly_DefinesNoTypeWithTheExactNameOfAnExistingDomainOrTransportModel()
    {
        var assembly = typeof(CompositionRoot).Assembly;
        var offenders = assembly.GetTypes().Where(t => ForbiddenExactTypeNames.Contains(t.Name)).ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ScanRequest_ReusesTheRealScanTargetType_DoesNotReimplementTargetIdentity()
    {
        var targetProperty = typeof(ServerSleuth.Gui.Models.ScanRequest).GetProperty(nameof(ServerSleuth.Gui.Models.ScanRequest.Target));
        Assert.NotNull(targetProperty);
        Assert.Equal(typeof(ServerSleuth.Core.Targets.ScanTarget), targetProperty!.PropertyType);
    }
}
