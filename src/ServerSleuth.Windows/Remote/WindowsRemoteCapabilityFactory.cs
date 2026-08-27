using ServerSleuth.Core.Targets;

namespace ServerSleuth.Windows.Remote;

/// <summary>
/// The architectural seam where a real <see cref="IWindowsRemoteCapabilities"/> implementation
/// is selected — the Windows-domain counterpart to
/// <see cref="ServerSleuth.Infrastructure.Targets.RemoteTargetTransportFactory"/>. Phase 10D-3A
/// left <see cref="Create"/> as an always-throwing seam for every remote platform (it still
/// does — there is no way to obtain the connection options/credentials a real implementation
/// needs from a bare <see cref="ScanTarget"/> alone, exactly like
/// <see cref="ServerSleuth.Infrastructure.Targets.RemoteTargetTransportFactory.Create"/> never
/// grew a body either). Phase 10D-3B fills the SAME seam Phase 10D-2 established the pattern
/// for: <see cref="CreateWinRm"/> below.
/// </summary>
public static class WindowsRemoteCapabilityFactory
{
    public static IWindowsRemoteCapabilities Create(ScanTarget target)
    {
        if (target.Kind != TargetKind.Remote)
        {
            throw new InvalidOperationException(
                $"{nameof(WindowsRemoteCapabilityFactory)} only handles {TargetKind.Remote} targets.");
        }

        if (target.Platform != TargetPlatform.Windows)
        {
            throw new NotSupportedException(
                $"{nameof(WindowsRemoteCapabilityFactory)} only handles {TargetPlatform.Windows} targets — " +
                $"'{target.Platform}' is not supported.");
        }

        throw new NotSupportedException(
            $"Windows remote scanning via WinRM is not implemented via {nameof(Create)}(ScanTarget) — " +
            $"use {nameof(CreateWinRm)}(target, options, credentialProvider) instead, which now implements it.");
    }

    /// <summary>
    /// Phase 10D-3B: the real Windows/WinRM remote capability implementation. Constructs (but
    /// does NOT connect) a <see cref="WinRmWindowsRemoteCapabilities"/> — the caller must still
    /// call its own <c>Connect(CancellationToken)</c> before use (skill.md §8: never connect
    /// until the scan actually begins, the same rule Phase 10D-2 established for SSH).
    /// </summary>
    public static WinRmWindowsRemoteCapabilities CreateWinRm(
        ScanTarget target, WinRmConnectionOptions options, IWindowsRemoteCredentialProvider credentialProvider)
    {
        if (target.Kind != TargetKind.Remote)
        {
            throw new InvalidOperationException(
                $"{nameof(CreateWinRm)} only handles {TargetKind.Remote} targets.");
        }

        if (target.Platform != TargetPlatform.Windows)
        {
            throw new NotSupportedException(
                $"{nameof(CreateWinRm)} only implements Windows targets — '{target.Platform}' is not supported.");
        }

        var session = new CimNetSession(options, credentialProvider);
        return new WinRmWindowsRemoteCapabilities(target, session, options.OperationTimeout);
    }
}
