using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Windows.Remote;

/// <summary>
/// The <see cref="ITargetTransport"/> registered for a remote Windows/WinRM scan — the
/// Windows-domain counterpart to
/// <see cref="ServerSleuth.Infrastructure.Remote.SshRemoteTargetTransport"/>, so
/// <c>ScanCommand</c>'s existing <c>services.GetRequiredService&lt;ITargetTransport&gt;()</c>
/// call (used for <c>DiscoveryContext.Target</c> and CLI target-identity output) works
/// identically for every target kind, without a Windows-specific special case in
/// <c>ScanCommand</c> beyond one more <c>is</c> check for <see cref="Connect"/> (mirroring the
/// existing SSH one exactly).
///
/// <see cref="ProcessRunner"/>/<see cref="FileSystemReader"/> are the inert
/// <see cref="UnavailableRemoteProcessRunner"/>/<see cref="UnavailableRemoteFileSystemReader"/>
/// — nothing <see cref="ServiceCollectionExtensions.AddServerSleuthWindowsRemote"/> registers
/// actually calls either through THIS property (the real remote data path is
/// <see cref="WinRmWindowsProviderSet"/>'s nine interfaces, registered separately); they exist
/// only to satisfy <see cref="ITargetTransport"/>'s shape without ever touching a local
/// resource.
/// </summary>
public sealed class WindowsRemoteTargetTransport(ScanTarget target, WinRmWindowsProviderSet providerSet) : ITargetTransport, IDisposable
{
    public ScanTarget Target { get; } = target;
    public IProcessRunner ProcessRunner { get; } = new UnavailableRemoteProcessRunner();
    public IFileSystemReader FileSystemReader { get; } = new UnavailableRemoteFileSystemReader();

    /// <summary>The nine local-shaped interfaces the composition root registers via
    /// <c>AddServerSleuthWindowsRemote</c> — exposed here so the composition root can obtain
    /// both this transport AND the provider set from one constructed object.</summary>
    public WinRmWindowsProviderSet ProviderSet { get; } = providerSet;

    public WinRmConnectResult Connect(CancellationToken cancellationToken)
    {
        try
        {
            ProviderSet.Connect(cancellationToken);
            return WinRmConnectResult.Ok();
        }
        catch (WinRmConnectException ex)
        {
            return WinRmConnectResult.Failure(ex.Status, ex.Message);
        }
        catch (OperationCanceledException)
        {
            return WinRmConnectResult.Failure(ServerSleuth.Infrastructure.Common.OperationStatus.Cancelled, "Connection cancelled.");
        }
    }

    public void Dispose() => ProviderSet.Dispose();
}
