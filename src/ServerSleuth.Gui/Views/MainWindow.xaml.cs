using System.Windows;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Views;

/// <summary>The application shell — title bar, left navigation, content region, status/footer
/// (skill.md GUI-1 §4). Contains no logic of its own beyond binding to <see cref="MainViewModel"/>
/// — every behavior lives in the ViewModel/service layer, testable without a live Window.</summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
