using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Infrastructure.Tests.Targets;

/// <summary>
/// Phase 10D-1 §10-12, §19-20: <see cref="RemoteTargetTransportFactory"/> is the "seam" a future
/// Windows (WinRM) / Linux (SSH) remote transport would plug into — these tests prove the seam
/// exists and is deterministic, and that calling it makes zero network connection of any kind
/// (it always throws before anything socket/HTTP/SSH/WinRM-shaped could run).
/// </summary>
public class RemoteTargetTransportFactoryTests
{
    [Fact]
    public void ResolveTransportKind_MapsWindowsToWinRm()
    {
        Assert.Equal(RemoteTransportKind.WinRm, RemoteTargetTransportFactory.ResolveTransportKind(TargetPlatform.Windows));
    }

    [Fact]
    public void ResolveTransportKind_MapsLinuxToSsh()
    {
        Assert.Equal(RemoteTransportKind.Ssh, RemoteTargetTransportFactory.ResolveTransportKind(TargetPlatform.Linux));
    }

    [Fact]
    public void ResolveTransportKind_IsDeterministic_AcrossManyCalls()
    {
        var results = Enumerable.Range(0, 50)
            .Select(_ => RemoteTargetTransportFactory.ResolveTransportKind(TargetPlatform.Windows))
            .Distinct()
            .ToList();

        Assert.Single(results);
    }

    [Fact]
    public void ResolveTransportKind_RejectsAnUnknownPlatform_RatherThanGuessing()
    {
        Assert.Throws<NotSupportedException>(() => RemoteTargetTransportFactory.ResolveTransportKind(TargetPlatform.Unknown));
    }

    [Fact]
    public void Create_ForALinuxRemoteTarget_ThrowsNotSupported_NeverConnects()
    {
        var target = ScanTarget.Remote("linux-host", TargetPlatform.Linux);
        var ex = Assert.Throws<NotSupportedException>(() => RemoteTargetTransportFactory.Create(target));
        Assert.Contains("Ssh", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ForAWindowsRemoteTarget_ThrowsNotSupported_NeverConnects()
    {
        var target = ScanTarget.Remote("windows-host", TargetPlatform.Windows);
        var ex = Assert.Throws<NotSupportedException>(() => RemoteTargetTransportFactory.Create(target));
        Assert.Contains("WinRm", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ForALocalTarget_RejectsIt_TheFactoryOnlyHandlesRemote()
    {
        Assert.Throws<InvalidOperationException>(() => RemoteTargetTransportFactory.Create(ScanTarget.Local()));
    }

    /// <summary>Phase 10D-1 §20: no socket/HTTP/SSH/WinRM call is made — proven by asserting the
    /// exact, immediate exception type rather than merely "it did not hang," and by the type-level
    /// no-network structural check in <see cref="NoNetworkGuaranteeTests"/>.</summary>
    [Fact]
    public void Create_NeverThrowsANetworkOrTimeoutRelatedException()
    {
        var target = ScanTarget.Remote("unreachable-host-that-must-never-be-contacted.invalid", TargetPlatform.Linux);
        var ex = Record.Exception(() => RemoteTargetTransportFactory.Create(target));

        Assert.IsType<NotSupportedException>(ex);
    }
}
