using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Infrastructure.Remote;

/// <summary>
/// The first real, non-Local <see cref="ITargetTransport"/> implementation — see skill.md
/// (Phase 10D-2) §1, §20. Connects lazily: nothing happens on the network until
/// <see cref="Connect"/> is called explicitly (skill.md §6: "Do not connect until the actual
/// scan begins"). Exposes the exact same <see cref="IProcessRunner"/>/<see cref="IFileSystemReader"/>
/// contracts every Linux scanner already depends on — <see cref="SshProcessRunner"/>/
/// <see cref="SshFileSystemReader"/> — so no scanner needed to change to become remote-capable.
/// </summary>
public sealed class SshRemoteTargetTransport : ITargetTransport, IDisposable
{
    private readonly ISshSession _session;

    public SshRemoteTargetTransport(ScanTarget target, ISshSession session)
    {
        if (target.Kind != TargetKind.Remote)
        {
            throw new InvalidOperationException($"{nameof(SshRemoteTargetTransport)} only serves {TargetKind.Remote} targets.");
        }

        Target = target;
        _session = session;
        ProcessRunner = new SshProcessRunner(session);
        FileSystemReader = new SshFileSystemReader(session);
    }

    public ScanTarget Target { get; }
    public IProcessRunner ProcessRunner { get; }
    public IFileSystemReader FileSystemReader { get; }

    /// <summary>Establishes the underlying SSH+SFTP connection — never called implicitly by the
    /// constructor or by any property getter above (skill.md §6).</summary>
    public SshConnectResult Connect(CancellationToken cancellationToken) => _session.Connect(cancellationToken);

    public void Dispose() => _session.Dispose();
}
