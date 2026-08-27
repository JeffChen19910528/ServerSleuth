using System.Security;

namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-2 §Step5, §Step8: the TRANSIENT, sensitive half of scan configuration — this type must
/// NEVER be held by <see cref="GuiApplicationState"/> (verified by
/// <c>GuiApplicationState_HasNoCredentialShapedProperty</c>), never flow through
/// <see cref="ServerSleuth.Gui.Navigation.INavigationService"/>, never be serialized, and never
/// be logged. <see cref="ScanConfigurationViewModel"/> holds the current instance as a private
/// field only, for exactly as long as the configuration workflow is on screen.
///
/// This is NOT a reuse of <c>ServerSleuth.Infrastructure.Remote.RemoteCredential</c>/
/// <c>ServerSleuth.Windows.Remote.WindowsRemoteCredential</c> — both live in projects
/// <c>ServerSleuth.Gui</c> does not (and, per its own established boundary, must not) reference.
/// A future GUI-3 phase, which IS allowed to reference <c>ServerSleuth.Infrastructure</c>/
/// <c>ServerSleuth.Windows</c> at the composition boundary where a real transport is actually
/// constructed, converts an instance of THIS type into the real credential type at the last
/// possible moment — this type itself never leaves the GUI project.
///
/// <see cref="Password"/> is a <see cref="SecureString"/> (never a plain <see cref="string"/>)
/// — the same discipline <c>WindowsRemoteCredential</c> already established; populated directly
/// from a WPF <see cref="System.Windows.Controls.PasswordBox.SecurePassword"/> read in
/// <c>ScanConfigurationView</c>'s code-behind, never through a bound string property (WPF's
/// <c>PasswordBox</c> deliberately has no bindable <c>Password</c> property, precisely so a
/// password can never end up sitting in a ViewModel's ordinary bound string property).
/// </summary>
public sealed record ScanCredentialInput
{
    public string? Username { get; init; }

    public SecureString? Password { get; init; }

    public static ScanCredentialInput Empty { get; } = new();

    public bool HasAnyValue => !string.IsNullOrEmpty(Username) || (Password?.Length ?? 0) > 0;

    /// <summary>Overrides the compiler-generated record <c>ToString()</c> (which would otherwise
    /// print <see cref="Username"/> — <see cref="SecureString"/> itself never prints its
    /// content, but this override is added defensively for the same reason
    /// <c>RemoteCredential.ToString()</c> was overridden: never trust the default.</summary>
    public override string ToString() => nameof(ScanCredentialInput);
}
