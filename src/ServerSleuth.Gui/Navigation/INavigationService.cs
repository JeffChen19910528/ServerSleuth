namespace ServerSleuth.Gui.Navigation;

/// <summary>
/// GUI-1 §6: an explicit navigation abstraction — the current page is always a single,
/// well-defined <see cref="NavigationPage"/> value, never inferred from which View happens to
/// be visible or from enumerating a collection. No View ever navigates another View directly;
/// every navigation request goes through this one seam, which <see cref="MainViewModel"/>
/// observes via <see cref="CurrentPageChanged"/> to update the bound content region.
/// </summary>
public interface INavigationService
{
    NavigationPage CurrentPage { get; }

    /// <summary>Raised after <see cref="CurrentPage"/> has changed — never raised if
    /// <see cref="NavigateTo"/> is called with the page that is already current (skill.md GUI-1
    /// §9's "deterministic navigation": navigating to the current page is a no-op, not a
    /// duplicate transition).</summary>
    event EventHandler<NavigationPage>? CurrentPageChanged;

    void NavigateTo(NavigationPage page);
}
