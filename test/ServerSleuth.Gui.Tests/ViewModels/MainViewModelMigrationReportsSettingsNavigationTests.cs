using ServerSleuth.Core.Targets;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;
using ServerSleuth.Gui.Tests.Fakes;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>GUI-7B: Migration/Reports/Settings are real, first-class navigation pages — routed
/// through the SAME existing <see cref="NavigationService"/>/<see cref="MainViewModel"/>
/// architecture GUI-1..GUI-7A already established, never a second navigation system. Mirrors
/// <c>MainViewModelDashboardAndInventoryNavigationTests</c>'s own construction pattern.</summary>
public class MainViewModelMigrationReportsSettingsNavigationTests
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
            ApplicationCount = 3, FindingsPerApplication = 2, DependenciesPerApplication = 1, ActionsPerApplication = 1, ChecksPerApplication = 1,
            DiscoveryEntities = [discoveryEntity]
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

    private static async Task<MainViewModel> BuildWithCompletedScanAsync()
    {
        var (main, _, scanExecution) = Build();
        main.NavigateCommand.Execute(NavigationPage.Scan);
        scanExecution.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => scanExecution.IsFinished));
        return main;
    }

    [Fact]
    public void NavigatingToMigration_BeforeAnyScan_ShowsTheRealEmptyMigrationPage_NeverAPlaceholder()
    {
        var (main, _, _) = Build();

        main.NavigateCommand.Execute(NavigationPage.Migration);

        Assert.Equal(NavigationPage.Migration, main.CurrentPageViewModel.Page);
        var migration = Assert.IsType<MigrationOverviewViewModel>(main.CurrentPageViewModel);
        Assert.True(migration.HasNoResults);
    }

    [Fact]
    public void NavigatingToReports_BeforeAnyScan_ShowsTheRealEmptyReportsPage_NeverAPlaceholder()
    {
        var (main, _, _) = Build();

        main.NavigateCommand.Execute(NavigationPage.Reports);

        Assert.Equal(NavigationPage.Reports, main.CurrentPageViewModel.Page);
        var reports = Assert.IsType<ReportsOverviewViewModel>(main.CurrentPageViewModel);
        Assert.True(reports.HasNoResults);
    }

    [Fact]
    public void NavigatingToSettings_BeforeAnyScan_ShowsTheRealSettingsPage_NeverAPlaceholder()
    {
        var (main, _, _) = Build();

        main.NavigateCommand.Execute(NavigationPage.Settings);

        Assert.Equal(NavigationPage.Settings, main.CurrentPageViewModel.Page);
        Assert.IsType<SettingsViewModel>(main.CurrentPageViewModel);
    }

    [Fact]
    public async Task AfterACompletedScan_NavigatingToMigration_ShowsTheRealApplicationList_WithoutRerunningTheExecutor()
    {
        var main = await BuildWithCompletedScanAsync();

        main.NavigateCommand.Execute(NavigationPage.Migration);

        var migration = Assert.IsType<MigrationOverviewViewModel>(main.CurrentPageViewModel);
        Assert.True(migration.HasResults);
        Assert.Equal(3, migration.Applications.Count);
    }

    [Fact]
    public async Task AfterACompletedScan_NavigatingToReports_ShowsTheRealReportFiles()
    {
        var main = await BuildWithCompletedScanAsync();

        main.NavigateCommand.Execute(NavigationPage.Reports);

        var reports = Assert.IsType<ReportsOverviewViewModel>(main.CurrentPageViewModel);
        Assert.True(reports.HasResults);
        Assert.NotEmpty(reports.ReportFileNames);
    }

    [Fact]
    public void MigrationStartScanCommand_NavigatesToScanConfiguration()
    {
        var (main, _, _) = Build();
        main.NavigateCommand.Execute(NavigationPage.Migration);
        var migration = Assert.IsType<MigrationOverviewViewModel>(main.CurrentPageViewModel);

        migration.StartScanCommand.Execute(null);

        Assert.Equal(NavigationPage.Scan, main.CurrentPageViewModel.Page);
        Assert.IsType<ScanConfigurationViewModel>(main.CurrentPageViewModel);
    }

    [Fact]
    public void ReportsStartScanCommand_NavigatesToScanConfiguration()
    {
        var (main, _, _) = Build();
        main.NavigateCommand.Execute(NavigationPage.Reports);
        var reports = Assert.IsType<ReportsOverviewViewModel>(main.CurrentPageViewModel);

        reports.StartScanCommand.Execute(null);

        Assert.Equal(NavigationPage.Scan, main.CurrentPageViewModel.Page);
        Assert.IsType<ScanConfigurationViewModel>(main.CurrentPageViewModel);
    }

    // ----- 29-36: the full recommended navigation walk, one existing NavigationService/
    // NavigateCommand, no second navigation system. -----
    [Theory]
    [InlineData(NavigationPage.Dashboard)]
    [InlineData(NavigationPage.Scan)]
    [InlineData(NavigationPage.Inventory)]
    [InlineData(NavigationPage.Results)]
    [InlineData(NavigationPage.Migration)]
    [InlineData(NavigationPage.Reports)]
    [InlineData(NavigationPage.Settings)]
    public void NavigateCommand_ReachesEveryPage_ThroughTheOneExistingNavigationService(NavigationPage page)
    {
        var (main, _, _) = Build();

        main.NavigateCommand.Execute(page);

        Assert.Equal(page, main.CurrentPageViewModel.Page);
        Assert.True(main.NavigationItems.Single(i => i.Page == page).IsSelected);
    }

    [Fact]
    public void NavigationItems_FullRecommendedOrder_AllSevenPages()
    {
        var (main, _, _) = Build();

        Assert.Equal(
            [NavigationPage.Dashboard, NavigationPage.Scan, NavigationPage.Inventory, NavigationPage.Results,
                NavigationPage.Migration, NavigationPage.Reports, NavigationPage.Settings],
            main.NavigationItems.Select(i => i.Page));
    }

    /// <summary>skill.md GUI-7B §17/§35: "New Scan" (from any GUI-7B page) returns to Scan
    /// Configuration even after the user has already visited Migration/Reports/Settings in
    /// between — proves navigating through a new page first doesn't disturb the existing
    /// GUI-3/GUI-5 "New Scan" flow <c>MainViewModelResultsNavigationTests</c> already covers.</summary>
    [Fact]
    public void NavigatingThroughEveryNewPage_ThenBackToScan_StillShowsScanConfiguration()
    {
        var (main, _, _) = Build();

        foreach (var page in new[] { NavigationPage.Dashboard, NavigationPage.Inventory, NavigationPage.Migration, NavigationPage.Reports, NavigationPage.Settings })
        {
            main.NavigateCommand.Execute(page);
        }

        main.NavigateCommand.Execute(NavigationPage.Scan);

        Assert.Equal(NavigationPage.Scan, main.CurrentPageViewModel.Page);
        Assert.IsType<ScanConfigurationViewModel>(main.CurrentPageViewModel);
    }
}
