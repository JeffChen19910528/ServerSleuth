using ServerSleuth.Gui.Navigation;

namespace ServerSleuth.Gui.ViewModels;

/// <summary>One entry in the left navigation area.</summary>
public sealed class NavigationItemViewModel(NavigationPage page, string label) : ObservableObject
{
    public NavigationPage Page { get; } = page;
    public string Label { get; } = label;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
