using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.Services;

/// <summary>The only <see cref="IApplicationStateService"/> implementation — starts from
/// <see cref="GuiApplicationState.Initial"/>, exactly the deterministic empty state GUI startup
/// must have (skill.md GUI-1 §12: opening the application must not start a scan, contact a
/// remote host, or produce any result — the initial snapshot reflects that literally: no
/// target, not scanning, no results, no error, nothing exportable).</summary>
public sealed class ApplicationStateService : IApplicationStateService
{
    public GuiApplicationState Current { get; private set; } = GuiApplicationState.Initial;

    public event EventHandler<GuiApplicationState>? StateChanged;

    public void Update(Func<GuiApplicationState, GuiApplicationState> transform)
    {
        Current = transform(Current);
        StateChanged?.Invoke(this, Current);
    }
}
