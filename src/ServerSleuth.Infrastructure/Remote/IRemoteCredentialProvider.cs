using ServerSleuth.Core.Targets;

namespace ServerSleuth.Infrastructure.Remote;

/// <summary>
/// Supplies a <see cref="RemoteCredential"/> for one connection attempt to one
/// <see cref="ScanTarget"/> — see skill.md (Phase 10D-2) §4. The minimum provider boundary, added
/// ONLY because <see cref="ISshSession"/> genuinely needs authentication material to connect;
/// Phase 10D-1 deliberately introduced no credential abstraction because nothing needed one yet.
/// Implementations decide where the credential actually lives (an already-loaded key file, an
/// environment variable, an OS credential store) — this codebase never persists or stores one
/// itself. Called once per connection attempt, never cached by the caller beyond that attempt's
/// lifetime.
/// </summary>
public interface IRemoteCredentialProvider
{
    RemoteCredential GetCredential(ScanTarget target);
}
