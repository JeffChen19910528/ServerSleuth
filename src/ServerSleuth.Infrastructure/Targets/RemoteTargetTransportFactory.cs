using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Remote;

namespace ServerSleuth.Infrastructure.Targets;

/// <summary>
/// The architectural seam where a remote <see cref="ITargetTransport"/> is selected — see
/// skill.md (Phase 10D-1) §10-12, (Phase 10D-2) §1, §20. Deliberately lives in Infrastructure,
/// not Core or Analysis (skill.md §10: "transport selection belongs in
/// Infrastructure/composition").
///
/// Phase 10D-1 left <see cref="Create"/> as an always-throwing seam for every remote platform.
/// Phase 10D-2 fills that seam ONLY for Linux/SSH — <see cref="CreateSsh"/> below — leaving
/// <see cref="Create"/> itself unchanged (it still throws for every platform, since it has no
/// way to obtain the connection options/credentials a real transport needs); Windows/WinRM
/// remains entirely unimplemented, still reached only through <see cref="Create"/>.
/// </summary>
public static class RemoteTargetTransportFactory
{
    /// <summary>Maps a platform to the transport protocol a future remote implementation for it
    /// would use — pure data, resolved with no network activity of any kind.</summary>
    public static RemoteTransportKind ResolveTransportKind(TargetPlatform platform) => platform switch
    {
        TargetPlatform.Windows => RemoteTransportKind.WinRm,
        TargetPlatform.Linux => RemoteTransportKind.Ssh,
        _ => throw new NotSupportedException(
            $"No remote transport is defined for target platform '{platform}'. " +
            "A remote target's platform must be known (Windows or Linux) before a transport can even be selected.")
    };

    /// <summary>
    /// Always throws. Present only so the "target → transport" selection point structurally
    /// exists in Infrastructure ahead of any real implementation — see the type-level doc
    /// comment. Never implement this method's body as an actual SSH/WinRM connection without
    /// first re-reading the Credential Boundary in ARCHITECTURE.md's Phase 10D-1 addendum.
    /// </summary>
    public static ITargetTransport Create(ScanTarget target)
    {
        if (target.Kind != TargetKind.Remote)
        {
            throw new InvalidOperationException(
                $"{nameof(RemoteTargetTransportFactory)} only handles {TargetKind.Remote} targets; " +
                $"'{target.Kind}' targets are served by {nameof(LocalTargetTransport)}.");
        }

        var transportKind = ResolveTransportKind(target.Platform);

        throw new NotSupportedException(
            $"Remote scanning via {transportKind} is not implemented via {nameof(Create)}(ScanTarget) — " +
            (transportKind == RemoteTransportKind.Ssh
                ? $"use {nameof(CreateSsh)}(target, options) instead, which now implements it."
                : "WinRM remains unimplemented (Phase 10D-2 only implemented Linux/SSH)."));
    }

    /// <summary>
    /// Phase 10D-2: the real Linux/SSH remote transport. Constructs (but does NOT connect) an
    /// <see cref="SshRemoteTargetTransport"/> — the caller must still call its own
    /// <c>Connect(CancellationToken)</c> before use (skill.md §6: never connect until the scan
    /// actually begins). Rejects a <see cref="TargetPlatform.Windows"/> target outright — this
    /// method implements ONLY Linux/SSH, per this phase's explicit scope.
    /// </summary>
    public static SshRemoteTargetTransport CreateSsh(ScanTarget target, SshConnectionOptions options)
    {
        if (target.Kind != TargetKind.Remote)
        {
            throw new InvalidOperationException(
                $"{nameof(CreateSsh)} only handles {TargetKind.Remote} targets; " +
                $"'{target.Kind}' targets are served by {nameof(LocalTargetTransport)}.");
        }

        if (target.Platform != TargetPlatform.Linux)
        {
            throw new NotSupportedException(
                $"{nameof(CreateSsh)} only implements Linux targets in Phase 10D-2 — " +
                $"'{target.Platform}' is not supported (Windows/WinRM remains unimplemented).");
        }

        return new SshRemoteTargetTransport(target, new SshNetSession(options));
    }
}
