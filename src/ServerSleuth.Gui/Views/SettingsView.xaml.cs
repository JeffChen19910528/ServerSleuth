using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    private void BrowseDefaultOutputDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var dialog = new OpenFolderDialog { Title = "Select Default Output Directory" };
        if (dialog.ShowDialog() == true)
        {
            viewModel.DefaultOutputDirectory = dialog.FolderName;
        }
    }
}
