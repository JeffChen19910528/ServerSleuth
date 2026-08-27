namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-2's presentation-layer mirror of <c>ServerSleuth.Windows.Remote.WindowsRemoteAuthenticationMechanism</c>
/// — see <see cref="ScanOutputFormat"/>'s doc comment for why a mirror, not a direct reference
/// (<c>ServerSleuth.Windows</c> is not referenced by <c>ServerSleuth.Gui</c>). Same three
/// members, same meaning (<c>Basic</c>/<c>Digest</c> are deliberately absent on the real enum
/// too — see its own doc comment — so they are absent here as well, never re-invented).
/// </summary>
public enum ScanWinRmAuthenticationMechanism
{
    Negotiate,
    Kerberos,
    CredSsp
}
