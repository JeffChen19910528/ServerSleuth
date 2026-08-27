namespace ServerSleuth.Windows.Remote;

/// <summary>
/// The closed set of WinRM/WS-Man authentication mechanisms this codebase supports — see
/// skill.md (Phase 10D-3B) §6. A deliberate SUBSET of what <c>Microsoft.Management.Infrastructure</c>'s
/// <c>PasswordAuthenticationMechanism</c> enum offers (which also has <c>Default</c>/<c>Digest</c>/
/// <c>Basic</c>): <c>Negotiate</c> (the standard "let Windows pick NTLM or Kerberos" mode, and
/// the correct default for a domain-joined target), <c>Kerberos</c> (explicit, for when mutual
/// domain trust must be enforced rather than silently falling back to NTLM), and
/// <c>CredSsp</c> (only useful for double-hop scenarios — supported because
/// <c>Microsoft.Management.Infrastructure</c> exposes it natively, not because this codebase
/// recommends it). <c>Basic</c>/<c>Digest</c> are deliberately NOT exposed: both send
/// credentials in a form that is either plaintext-equivalent or weakly hashed unless the
/// connection is already TLS-protected, and neither is a common real Windows Server
/// configuration — omitting them is a documented decision, not an oversight (skill.md §6:
/// "implement only what the selected transport safely supports and what can be tested").
/// </summary>
public enum WindowsRemoteAuthenticationMechanism
{
    Negotiate,
    Kerberos,
    CredSsp
}
