using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;
using ServerSleuth.Gui.Tests.Fakes;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>GUI-1 §4, §6, §9: the shell ViewModel's navigation-item list, current-page binding,
/// and status text — all target-agnostic, all deterministic.</summary>
public class MainViewModelTests
{
    private static MainViewModel Build(out NavigationService navigation, out ApplicationStateService state)
    {
        navigation = new NavigationService();
        state = new ApplicationStateService();
        var scanConfiguration = new ScanConfigurationViewModel(new ScanConfigurationValidator(), new ScanRequestFactory());
        var scanExecution = new ScanExecutionViewModel(new FakeGuiScanExecutor());
        return new MainViewModel(navigation, state, scanConfiguration, scanExecution);
    }

    // GUI-7A: Inventory was added as a first-class navigation item, in the recommended
    // Dashboard/Scan/Inventory/Results/Migration/Reports/Settings order.
    [Fact]
    public void NavigationItems_HasExactlySevenItems_InTheDocumentedOrder()
    {
        var viewModel = Build(out _, out _);

        Assert.Equal(
        [
            NavigationPage.Dashboard, NavigationPage.Scan, NavigationPage.Inventory, NavigationPage.Results,
            NavigationPage.Migration, NavigationPage.Reports, NavigationPage.Settings
        ], viewModel.NavigationItems.Select(i => i.Page));
    }

    [Fact]
    public void CurrentPageViewModel_StartsOnDashboard()
    {
        var viewModel = Build(out _, out _);
        Assert.Equal(NavigationPage.Dashboard, viewModel.CurrentPageViewModel.Page);
        Assert.True(viewModel.NavigationItems.Single(i => i.Page == NavigationPage.Dashboard).IsSelected);
    }

    [Theory]
    [InlineData(NavigationPage.Scan)]
    [InlineData(NavigationPage.Inventory)]
    [InlineData(NavigationPage.Results)]
    [InlineData(NavigationPage.Migration)]
    [InlineData(NavigationPage.Reports)]
    [InlineData(NavigationPage.Settings)]
    public void NavigateCommand_ToEachPage_UpdatesCurrentPageViewModel_AndSelection(NavigationPage page)
    {
        var viewModel = Build(out _, out _);

        viewModel.NavigateCommand.Execute(page);

        Assert.Equal(page, viewModel.CurrentPageViewModel.Page);
        Assert.True(viewModel.NavigationItems.Single(i => i.Page == page).IsSelected);
        Assert.All(viewModel.NavigationItems.Where(i => i.Page != page), i => Assert.False(i.IsSelected));
    }

    [Fact]
    public void StatusText_ReflectsNoTargetSelected_Initially()
    {
        var viewModel = Build(out _, out _);
        Assert.Contains("No target selected", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusText_ReflectsAnErrorMessage_WhenStatePublishesOne()
    {
        var viewModel = Build(out _, out var state);

        state.Update(s => s with { LastErrorMessage = "An unexpected error occurred. See application logs for details." });

        Assert.StartsWith("Error:", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusText_ReflectsScanRunningState()
    {
        var viewModel = Build(out _, out var state);

        state.Update(s => s with { IsScanRunning = true });

        Assert.Contains("Scanning", viewModel.StatusText, StringComparison.Ordinal);
    }

    // GUI-2: the Scan page now shows a real ScanConfigurationViewModel, not a placeholder.
    [Fact]
    public void NavigateCommand_ToScan_ShowsTheRealScanConfigurationViewModel_NotAPlaceholder()
    {
        var viewModel = Build(out _, out _);

        viewModel.NavigateCommand.Execute(NavigationPage.Scan);

        Assert.IsType<ScanConfigurationViewModel>(viewModel.CurrentPageViewModel);
    }

    // GUI-7A: Dashboard is now a real page (DashboardOverviewViewModel), never a placeholder.
    [Fact]
    public void NavigateCommand_ToScan_ReturnsToDashboard_ShowsTheRealDashboardOverview_NotAPlaceholder()
    {
        var viewModel = Build(out _, out _);

        viewModel.NavigateCommand.Execute(NavigationPage.Scan);
        viewModel.NavigateCommand.Execute(NavigationPage.Dashboard);

        Assert.IsType<DashboardOverviewViewModel>(viewModel.CurrentPageViewModel);
    }

    [Fact]
    public void VersionText_IsNeverEmpty()
    {
        var viewModel = Build(out _, out _);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.VersionText));
    }
}
