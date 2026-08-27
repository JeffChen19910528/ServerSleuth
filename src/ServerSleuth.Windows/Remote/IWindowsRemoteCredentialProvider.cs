namespace ServerSleuth.Windows.Remote;

/// <summary>
/// Supplies the one <see cref="WindowsRemoteCredential"/> a WinRM connection attempt needs —
/// mirrors <see cref="ServerSleuth.Infrastructure.Remote.IRemoteCredentialProvider"/>'s shape
/// exactly (skill.md Phase 10D-3B §5). Never persists, never logs, never serializes.
/// </summary>
public interface IWindowsRemoteCredentialProvider
{
    WindowsRemoteCredential GetCredential();
}

/// <summary>
/// Holds one already-resolved credential in memory — sufficient because the CLI resolves
/// credential material exactly once, up front, from exactly the sources the user named
/// (skill.md §5), the same pattern
/// <see cref="ServerSleuth.Infrastructure.Remote.StaticRemoteCredentialProvider"/> already
/// established for SSH.
/// </summary>
public sealed class StaticWindowsRemoteCredentialProvider(WindowsRemoteCredential credential) : IWindowsRemoteCredentialProvider
{
    public WindowsRemoteCredential GetCredential() => credential;
}
