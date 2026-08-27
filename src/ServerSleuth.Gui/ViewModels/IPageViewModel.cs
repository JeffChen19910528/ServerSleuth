using ServerSleuth.Gui.Navigation;

namespace ServerSleuth.Gui.ViewModels;

/// <summary>The common shape every page ViewModel exposes to <see cref="MainViewModel"/> —
/// introduced in GUI-2 because <see cref="MainViewModel.CurrentPageViewModel"/> must now hold
/// either a <see cref="PlaceholderPageViewModel"/> (five of the six pages, still placeholders)
/// or a real <see cref="ScanConfigurationViewModel"/> (the Scan page). Deliberately minimal —
/// just enough for <c>MainViewModel</c> to know which <see cref="NavigationPage"/> a given
/// instance represents.</summary>
public interface IPageViewModel
{
    NavigationPage Page { get; }
}
