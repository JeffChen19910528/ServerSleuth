using ServerSleuth.Gui.Navigation;

namespace ServerSleuth.Gui.ViewModels;

/// <summary>One entry in the left navigation area.</summary>
public sealed class NavigationItemViewModel(NavigationPage page, string label) : ObservableObject
{
    public NavigationPage Page { get; } = page;

    private string _label = label;

    /// <summary>Settable (not just constructor-initialized) so GUI-7's language toggle can
    /// update the already-displayed nav item text in place without rebuilding the collection
    /// and losing <see cref="IsSelected"/>.</summary>
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
