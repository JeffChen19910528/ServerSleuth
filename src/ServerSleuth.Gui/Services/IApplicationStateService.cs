using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.Services;

/// <summary>
/// Holds the single current <see cref="GuiApplicationState"/> snapshot and publishes a change
/// event whenever <see cref="Update"/> replaces it — the one place any part of the GUI may read
/// or transition application-wide state (skill.md GUI-1 §5-6: "no global static singleton state
/// unless absolutely necessary" — this IS that one necessary seam, injected via DI rather than
/// a static, so it is fakeable in tests).
/// </summary>
public interface IApplicationStateService
{
    GuiApplicationState Current { get; }

    event EventHandler<GuiApplicationState>? StateChanged;

    /// <summary>Applies <paramref name="transform"/> to the current snapshot and publishes the
    /// result — the ONLY way state ever changes, so every transition is a single, traceable
    /// call site rather than scattered property setters.</summary>
    void Update(Func<GuiApplicationState, GuiApplicationState> transform);
}
