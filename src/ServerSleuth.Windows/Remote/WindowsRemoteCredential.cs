using System.Security;

namespace ServerSleuth.Windows.Remote;

/// <summary>
/// The WinRM authentication material a real <see cref="CimNetSession"/> connection needs — the
/// Windows-domain counterpart to <see cref="ServerSleuth.Infrastructure.Remote.RemoteCredential"/>
/// (skill.md Phase 10D-3B §5). Deliberately transport-layer only: never a property of
/// <see cref="ServerSleuth.Core.Targets.ScanTarget"/>/<c>DiscoveryContext</c>/any domain or
/// report model (skill.md §5 restates the same boundary Phase 10D-2 already established for
/// SSH).
///
/// <see cref="Password"/> is a <see cref="SecureString"/> — the exact type
/// <c>Microsoft.Management.Infrastructure.Options.CimCredential</c>'s own constructor requires,
/// so no intermediate plain-<see cref="string"/> holding the password needs to exist anywhere
/// in this codebase between "read from the CLI" and "handed to the CIM library."
///
/// A genuine near-miss caught before shipping, exactly like Phase 10D-2's
/// <c>RemoteCredential.ToString()</c> finding: a plain <c>sealed record</c>'s
/// compiler-generated <see cref="object.ToString"/> prints every property's raw value — for a
/// <see cref="SecureString"/> property that would only print its type name (harmless), but
/// <see cref="UserName"/>/<see cref="Domain"/> are still identity information worth keeping out
/// of an accidental log line, so <see cref="ToString"/> is explicitly overridden here too, for
/// the same "never trust the default" discipline.
/// </summary>
public sealed record WindowsRemoteCredential
{
    public string? Domain { get; init; }
    public required string UserName { get; init; }
    public required SecureString Password { get; init; }
    public WindowsRemoteAuthenticationMechanism AuthenticationMechanism { get; init; } = WindowsRemoteAuthenticationMechanism.Negotiate;

    public override string ToString() =>
        $"{nameof(WindowsRemoteCredential)} {{ UserName = {UserName}, AuthenticationMechanism = {AuthenticationMechanism} }}";
}
