using ServerSleuth.Core.Targets;

namespace ServerSleuth.Infrastructure.Remote;

/// <summary>
/// The simplest <see cref="IRemoteCredentialProvider"/>: always returns the SAME, already-built
/// <see cref="RemoteCredential"/> — sufficient for a caller (e.g. the CLI composition root) that
/// already resolved credential material once, up front, from an explicit user-supplied source
/// (a key file path, an environment variable) before a single scan begins. Holds the credential
/// only in memory for the process's lifetime — never writes it anywhere.
/// </summary>
public sealed class StaticRemoteCredentialProvider(RemoteCredential credential) : IRemoteCredentialProvider
{
    public RemoteCredential GetCredential(ScanTarget target) => credential;
}
