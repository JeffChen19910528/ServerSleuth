namespace ServerSleuth.Gui.Navigation;

/// <summary>
/// The only <see cref="INavigationService"/> implementation — a plain, single field holding the
/// current page, never a <c>Dictionary</c>/<c>HashSet</c> of visited pages whose enumeration
/// order could leak into observable behavior (skill.md GUI-1 §6's explicit "does not depend on
/// Dictionary enumeration order"). Starts on <see cref="NavigationPage.Dashboard"/> — the
/// deterministic default every session begins from.
/// </summary>
public sealed class NavigationService : INavigationService
{
    public NavigationPage CurrentPage { get; private set; } = NavigationPage.Dashboard;

    public event EventHandler<NavigationPage>? CurrentPageChanged;

    public void NavigateTo(NavigationPage page)
    {
        if (page == CurrentPage)
        {
            return;
        }

        CurrentPage = page;
        CurrentPageChanged?.Invoke(this, page);
    }
}
