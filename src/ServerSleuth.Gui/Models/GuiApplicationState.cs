using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Navigation;

namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-1 §5: the whole application's observable state, as ONE immutable snapshot — every
/// transition produces a new instance via the `with` expression (a sealed record, the same
/// convention every domain/result type in this solution already uses), never in-place mutation.
/// This makes state transitions trivially testable (compare two snapshots) and impossible to
/// observe half-updated.
///
/// <see cref="Target"/> reuses the EXISTING <see cref="ScanTarget"/> domain type directly rather
/// than a raw display-name string — <see cref="ScanTarget"/> already carries zero credential
/// fields (mechanically verified since Phase 10C, reconfirmed by this phase's own
/// <c>GuiApplicationState_HasNoCredentialShapedProperty</c> test) and already has a working
/// <c>Id</c>/<c>DisplayName</c> shape the GUI can bind to once target selection is implemented
/// in a later phase — reusing it here is exactly the "consume the existing domain abstraction,
/// never reinvent it" rule GUI-1's own Critical Dependency Rule states.
///
/// **Deliberately holds NO credential field of any kind, and never will** — see
/// ARCHITECTURE.md's GUI-1 addendum, "Application State" section, for the full reasoning this
/// mirrors from <see cref="ScanTarget"/>'s own doc comment.
/// </summary>
public sealed record GuiApplicationState
{
    public NavigationPage CurrentPage { get; init; } = NavigationPage.Dashboard;

    /// <summary>The target a future scan would run against — <c>null</c> until target selection
    /// (a later GUI phase) sets one. Never a credential-bearing type.</summary>
    public ScanTarget? Target { get; init; }

    public bool IsScanRunning { get; init; }

    public bool HasResults { get; init; }

    /// <summary>A concise, user-safe message only — never a raw exception/stack trace (skill.md
    /// GUI-1 §8's error-boundary requirement). Set by the application-level exception boundary
    /// or by a future scan-failure path, never by binding an exception object directly.</summary>
    public string? LastErrorMessage { get; init; }

    public bool IsExportAvailable { get; init; }

    public static GuiApplicationState Initial { get; } = new();
}
