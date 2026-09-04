using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Views;

public partial class ResultsDashboardView : UserControl
{
    public ResultsDashboardView()
    {
        InitializeComponent();
    }

    private ResultsDashboardViewModel? ViewModel => DataContext as ResultsDashboardViewModel;

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
