using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
    }

    private ReportsOverviewViewModel? ViewModel => DataContext as ReportsOverviewViewModel;

    private void BrowseExportDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var dialog = new OpenFolderDialog { Title = "Select Export Directory" };
        if (dialog.ShowDialog() == true)
        {
            viewModel.ExportDirectory = dialog.FolderName;
        }
    }
}
