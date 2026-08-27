namespace ServerSleuth.Windows.Remote;

/// <summary>
/// Everything a <see cref="CimNetSession"/> needs to attempt a WinRM/WS-Man connection — the
/// Windows-domain counterpart to
/// <see cref="ServerSleuth.Infrastructure.Remote.SshConnectionOptions"/>. Carries no credential
/// of its own (that is <see cref="IWindowsRemoteCredentialProvider"/>'s job — kept as a
/// SEPARATE constructor argument everywhere this type is consumed, never merged into it, so a
/// connection-options value is safe to log/describe on its own).
///
/// <see cref="UseSsl"/> defaults to <c>true</c> — TLS is the default, not opt-in (skill.md §7:
/// "server certificate validation must fail closed by default"). Setting it <c>false</c> is a
/// legitimate, common real-world WinRM configuration (the classic port-5985 HTTP listener,
/// which still encrypts every message at the WS-Man layer via Negotiate/Kerberos — never
/// plaintext), but it is never the DEFAULT this codebase picks for the caller.
///
/// Deliberately has NO field to disable certificate validation, NO field to accept any
/// certificate, and NO field to disable message encryption — see <see cref="CimNetSession"/>'s
/// doc comment for exactly which <c>WSManSessionOptions</c> flags are hard-coded, not exposed
/// as configuration, to make a silent security bypass structurally impossible.
/// </summary>
public sealed record WinRmConnectionOptions
{
    public required string Host { get; init; }
    public int? Port { get; init; }
    public bool UseSsl { get; init; } = true;
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int ResolvedPort => Port ?? (UseSsl ? 5986 : 5985);
}
