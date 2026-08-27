using System.Windows.Controls;

namespace ServerSleuth.Gui.Views;

/// <summary>The reusable view every placeholder page (skill.md GUI-1 §4) renders through — see
/// <see cref="ServerSleuth.Gui.ViewModels.PlaceholderPageViewModel"/>'s own doc comment for why
/// one shared View/ViewModel pair is used instead of six near-identical pairs.</summary>
public partial class PlaceholderView : UserControl
{
    public PlaceholderView() => InitializeComponent();
}
