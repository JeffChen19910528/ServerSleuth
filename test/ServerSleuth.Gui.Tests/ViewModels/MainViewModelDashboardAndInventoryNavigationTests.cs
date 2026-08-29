using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;
using ServerSleuth.Gui.Tests.Fakes;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>GUI-7A: Dashboard and Inventory are now real, first-class navigation pages —
/// routed through the SAME existing <see cref="NavigationService"/>/<see cref="MainViewModel"/>
/// architecture GUI-1 already established, never a second navigation system. Mirrors
/// <c>MainViewModelResultsNavigationTests</c>'s own construction pattern.</summary>
public class MainViewModelDashboardAndInventoryNavigationTests
{
    private static ScanRequest LocalRequest() => new()
    {
        Target = ScanTarget.Local(TargetPlatform.Windows),
        OutputDirectory = "./out",
        OutputFormat = ScanOutputFormat.Both,
        OverwritePolicy = ScanOverwritePolicy.FailIfExists,
        Verbose = false
    };

    private static (MainViewModel MainViewModel, FakeGuiScanExecutor Executor, ScanExecutionViewModel ScanExecution) Build()
    {
        var navigation = new NavigationService();
        var state = new ApplicationStateService();
        var scanConfiguration = new ScanConfigurationViewModel(new ScanConfigurationValidator(), new ScanRequestFactory());
        var discoveryEntity = new Service
        {
            Id = "service:fixture", Name = "FixtureService", Type = "Service", Source = "ServiceControlManager",
            Status = EntityStatus.Running, Confidence = Confidence.VeryHigh()
        };
        var completedState = ScanResultFixtureFactory.BuildCompletedState(new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 3, FindingsPerApplication = 2, DiscoveryEntities = [discoveryEntity]
        });
        var executor = new FakeGuiScanExecutor
        {
            CompletionToReturn = new ScanCompletionState
            {
                Status = completedState.Status,
                EntityCount = completedState.EntityCount,
                ErrorCount = completedState.ErrorCount,
                ScannerStatuses = completedState.ScannerStatuses,
                OutputPaths = completedState.OutputPaths,
                PipelineResult = completedState.PipelineResult
            }
        };
        var scanExecution = new ScanExecutionViewModel(executor);
        var main = new MainViewModel(navigation, state, scanConfiguration, scanExecution);
        return (main, executor, scanExecution);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(10);
        }

        return condition();
    }

    [Fact]
    public void OnStartup_BeforeAnyScan_DashboardIsTheRealOverviewPage_ShowingTheEmptyState()
    {
        var (main, _, _) = Build();

        Assert.Equal(NavigationPage.Dashboard, main.CurrentPageViewModel.Page);
        var dashboard = Assert.IsType<DashboardOverviewViewModel>(main.CurrentPageViewModel);
        Assert.True(dashboard.HasNoResults);
    }

    [Fact]
    public void NavigatingToInventory_BeforeAnyScan_ShowsTheRealEmptyInventoryPage_NeverAPlaceholder()
    {
        var (main, _, _) = Build();

        main.NavigateCommand.Execute(NavigationPage.Inventory);

        Assert.Equal(NavigationPage.Inventory, main.CurrentPageViewModel.Page);
        var inventory = Assert.IsType<InventoryExplorerViewModel>(main.CurrentPageViewModel);
        Assert.True(inventory.HasNoInventory);
    }

    [Fact]
    public async Task AfterACompletedScan_NavigatingToDashboard_ShowsTheRealSummary_WithoutRerunningTheExecutor()
    {
        var (main, executor, scanExecution) = Build();
        main.NavigateCommand.Execute(NavigationPage.Scan);
        scanExecution.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => scanExecution.IsFinished));

        main.NavigateCommand.Execute(NavigationPage.Dashboard);

        var dashboard = Assert.IsType<DashboardOverviewViewModel>(main.CurrentPageViewModel);
        Assert.True(dashboard.HasResults);
        Assert.Equal(3, dashboard.ApplicationCount);
        Assert.Single(executor.Calls);
    }

    [Fact]
    public async Task AfterACompletedScan_NavigatingToInventory_ShowsTheRealDiscoveredEntities()
    {
        var (main, _, scanExecution) = Build();
        main.NavigateCommand.Execute(NavigationPage.Scan);
        scanExecution.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => scanExecution.IsFinished));

        main.NavigateCommand.Execute(NavigationPage.Inventory);

        var inventory = Assert.IsType<InventoryExplorerViewModel>(main.CurrentPageViewModel);
        Assert.False(inventory.HasNoInventory);
    }

    [Fact]
    public async Task DashboardViewResultsCommand_BuildsAndShowsTheRealResultsDashboard()
    {
        var (main, executor, scanExecution) = Build();
        main.NavigateCommand.Execute(NavigationPage.Scan);
        scanExecution.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => scanExecution.IsFinished));
        main.NavigateCommand.Execute(NavigationPage.Dashboard);
        var dashboard = Assert.IsType<DashboardOverviewViewModel>(main.CurrentPageViewModel);

        dashboard.ViewResultsCommand.Execute(null);

        Assert.Equal(NavigationPage.Results, main.CurrentPageViewModel.Page);
        Assert.IsType<ResultsDashboardViewModel>(main.CurrentPageViewModel);
        Assert.Single(executor.Calls); // View Results never re-ran the scan.
    }

    [Fact]
    public void DashboardViewInventoryCommand_NavigatesToInventory()
    {
        var (main, _, _) = Build();
        var dashboard = Assert.IsType<DashboardOverviewViewModel>(main.CurrentPageViewModel);

        dashboard.ViewInventoryCommand.Execute(null);

        Assert.Equal(NavigationPage.Inventory, main.CurrentPageViewModel.Page);
    }

    [Fact]
    public void DashboardStartScanCommand_NavigatesToScanConfiguration()
    {
        var (main, _, _) = Build();
        var dashboard = Assert.IsType<DashboardOverviewViewModel>(main.CurrentPageViewModel);

        dashboard.StartScanCommand.Execute(null);

        Assert.Equal(NavigationPage.Scan, main.CurrentPageViewModel.Page);
        Assert.IsType<ScanConfigurationViewModel>(main.CurrentPageViewModel);
    }

    // GUI-7A's own scope boundary test here ("Migration/Reports/Settings remain placeholders")
    // was removed in GUI-7B — that phase explicitly, intentionally makes all three real pages;
    // see MainViewModelMigrationReportsSettingsNavigationTests for their replacement coverage.

    [Fact]
    public void NavigationItems_IncludeInventory_InTheRecommendedOrder()
    {
        var (main, _, _) = Build();

        var pages = main.NavigationItems.Select(i => i.Page).ToList();

        Assert.Equal(
            [NavigationPage.Dashboard, NavigationPage.Scan, NavigationPage.Inventory, NavigationPage.Results,
                NavigationPage.Migration, NavigationPage.Reports, NavigationPage.Settings],
            pages);
    }
}
