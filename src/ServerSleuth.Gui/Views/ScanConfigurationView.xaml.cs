using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Views;

/// <summary>
/// GUI-2 §5, §8: the ONLY place a password ever exists as plaintext-adjacent UI state is this
/// code-behind's <see cref="PasswordBox"/> control itself (WPF's own built-in masking) —
/// <see cref="PasswordBox_PasswordChanged"/> reads <see cref="PasswordBox.SecurePassword"/>
/// directly and hands it straight to <see cref="ScanConfigurationViewModel.SetPassword"/>, which
/// never exposes it as a bound string property. No other code in this class touches the
/// password at all.
/// </summary>
public partial class ScanConfigurationView : UserControl
{
    public ScanConfigurationView() => InitializeComponent();

    private ScanConfigurationViewModel? ViewModel => DataContext as ScanConfigurationViewModel;

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) =>
        ViewModel?.SetPassword(PasswordBox.SecurePassword);

    private void LocalRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.TargetKind = TargetKind.Local;
        }
    }

    private void RemoteRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.TargetKind = TargetKind.Remote;
        }
    }

    private void BrowseSshPrivateKey_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var dialog = new OpenFileDialog { Title = "Select SSH Private Key", Filter = "All files (*.*)|*.*" };
        if (dialog.ShowDialog() == true)
        {
            viewModel.SshPrivateKeyPath = dialog.FileName;
        }
    }

    private void BrowseOutputDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var dialog = new OpenFolderDialog { Title = "Select Output Directory" };
        if (dialog.ShowDialog() == true)
        {
            viewModel.OutputDirectory = dialog.FolderName;
        }
    }
}
