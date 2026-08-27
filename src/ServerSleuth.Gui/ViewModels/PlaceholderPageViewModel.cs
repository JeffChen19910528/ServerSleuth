using ServerSleuth.Gui.Navigation;

namespace ServerSleuth.Gui.ViewModels;

/// <summary>
/// GUI-1 §Objective: every page is a placeholder in this phase — one reusable ViewModel shape
/// (title + description text) is enough to prove the navigation/content-region architecture
/// works for all six pages, rather than six near-identical empty ViewModel classes (skill.md
/// GUI-1's own "avoid premature abstractions" instruction). A LATER phase replaces the
/// placeholder for a given <see cref="Page"/> with a real, feature-specific ViewModel — this
/// type is intentionally disposable, not a foundation the real pages will be built on top of.
/// </summary>
public sealed class PlaceholderPageViewModel(NavigationPage page, string title, string description) : ObservableObject, IPageViewModel
{
    public NavigationPage Page { get; } = page;
    public string Title { get; } = title;
    public string Description { get; } = description;
}
