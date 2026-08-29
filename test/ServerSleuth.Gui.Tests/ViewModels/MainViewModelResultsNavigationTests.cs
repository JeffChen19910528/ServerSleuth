using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;
using ServerSleuth.Gui.Tests.Fakes;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>GUI-4 §Step4, §Step24: Scan Execution → "View Results" → Results Dashboard, and
/// "New Scan" → Scan Configuration — both routed through the existing
/// NavigationService/MainViewModel architecture (no second navigation system), and neither path
/// ever re-invokes <see cref="IGuiScanExecutor"/> a second time.</summary>
public class MainViewModelResultsNavigationTests
{
    private static ScanRequest LocalRequest() => new()
    {
        Target = ScanTarget.Local(TargetPlatform.Windows),
        OutputDirectory = "./out",
        OutputFormat = ScanOutputFormat.Both,
        OverwritePolicy = ScanOverwritePolicy.FailIfExists,
        Verbose = false
    };

    private static (MainViewModel MainViewModel, FakeGuiScanExecutor Executor, ScanExecutionViewModel ScanExecution) Build(
        FakeGuiReportExportService? exportService = null, FakeGuiReportViewerService? viewerService = null)
    {
        var navigation = new NavigationService();
        var state = new ApplicationStateService();
        var scanConfiguration = new ScanConfigurationViewModel(new ScanConfigurationValidator(), new ScanRequestFactory());
        var completedState = ScanResultFixtureFactory.BuildCompletedState(new ScanResultFixtureFactory.Options { ApplicationCount = 2, FindingsPerApplication = 1 });
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
        var main = new MainViewModel(navigation, state, scanConfiguration, scanExecution, exportService, viewerService);
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
    public async Task ViewResults_AfterACompletedScan_ShowsTheRealResultsDashboardViewModel()
    {
        var (main, executor, scanExecution) = Build();
        main.NavigateCommand.Execute(NavigationPage.Scan);
        scanExecution.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => scanExecution.IsFinished));

        scanExecution.ViewResultsCommand.Execute(null);

        Assert.Equal(NavigationPage.Results, main.CurrentPageViewModel.Page);
        var dashboard = Assert.IsType<ResultsDashboardViewModel>(main.CurrentPageViewModel);
        Assert.True(dashboard.HasResults);
        Assert.Equal(2, dashboard.Applications.Count);
        Assert.Single(executor.Calls); // exactly one execution — "View Results" never re-ran anything.
    }

    [Fact]
    public async Task NavigatingAwayFromResults_AndBack_NeverRebuildsTheDashboard_OrReRunsTheExecutor()
    {
        var (main, executor, scanExecution) = Build();
        main.NavigateCommand.Execute(NavigationPage.Scan);
        scanExecution.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => scanExecution.IsFinished));
        scanExecution.ViewResultsCommand.Execute(null);

        var firstDashboard = main.CurrentPageViewModel;

        main.NavigateCommand.Execute(NavigationPage.Dashboard);
        main.NavigateCommand.Execute(NavigationPage.Results);

        Assert.Same(firstDashboard, main.CurrentPageViewModel);
        Assert.Single(executor.Calls);
    }

    [Fact]
    public async Task SwitchingTabsWithinTheApp_NeverTriggersPipelineExecution()
    {
        var (main, executor, scanExecution) = Build();
        main.NavigateCommand.Execute(NavigationPage.Scan);
        scanExecution.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => scanExecution.IsFinished));
        scanExecution.ViewResultsCommand.Execute(null);

        foreach (var page in new[] { NavigationPage.Dashboard, NavigationPage.Migration, NavigationPage.Reports, NavigationPage.Settings, NavigationPage.Results })
        {
            main.NavigateCommand.Execute(page);
        }

        Assert.Single(executor.Calls);
    }

    [Fact]
    public async Task NewScan_AfterViewingResults_ReturnsToScanConfiguration()
    {
        var (main, _, scanExecution) = Build();
        main.NavigateCommand.Execute(NavigationPage.Scan);
        scanExecution.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => scanExecution.IsFinished));
        scanExecution.ViewResultsCommand.Execute(null);

        main.NavigateCommand.Execute(NavigationPage.Scan);
        scanExecution.NewScanCommand.Execute(null);

        Assert.IsType<ScanConfigurationViewModel>(main.CurrentPageViewModel);
    }

    /// <summary>GUI-5 §3: "New Scan" is also reachable directly from the Results Dashboard
    /// itself (not only from the Scan Execution page's own completion buttons) — routed through
    /// the exact same existing navigation flow, never a second one.</summary>
    [Fact]
    public async Task NewScan_FromTheResultsDashboardItself_ReturnsToScanConfiguration()
    {
        var (main, _, scanExecution) = Build();
        main.NavigateCommand.Execute(NavigationPage.Scan);
        scanExecution.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => scanExecution.IsFinished));
        scanExecution.ViewResultsCommand.Execute(null);

        var dashboard = Assert.IsType<ResultsDashboardViewModel>(main.CurrentPageViewModel);
        dashboard.NewScanCommand.Execute(null);

        Assert.Equal(NavigationPage.Scan, main.CurrentPageViewModel.Page);
        Assert.IsType<ScanConfigurationViewModel>(main.CurrentPageViewModel);
    }

    /// <summary>GUI-5 §7: "Exporting JSON + HTML does not execute the scan twice" — nor does a
    /// single export at all; <see cref="IGuiScanExecutor"/> is never touched by the export path.</summary>
    [Fact]
    public async Task ExportingJsonThenHtml_FromTheDashboard_NeverInvokesTheScanExecutor()
    {
        var exportService = new FakeGuiReportExportService();
        var (main, executor, scanExecution) = Build(exportService);
        main.NavigateCommand.Execute(NavigationPage.Scan);
        scanExecution.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => scanExecution.IsFinished));
        scanExecution.ViewResultsCommand.Execute(null);

        var dashboard = Assert.IsType<ResultsDashboardViewModel>(main.CurrentPageViewModel);
        dashboard.ExportFormat = ScanOutputFormat.Json;
        dashboard.ExportReportCommand.Execute(null);
        dashboard.ExportFormat = ScanOutputFormat.Html;
        dashboard.ExportReportCommand.Execute(null);

        Assert.Equal(2, exportService.Calls.Count);
        Assert.Single(executor.Calls); // exactly the one original scan — export never re-ran it.
    }

    /// <summary>GUI-5 §7: "Opening a report does not invoke a scan."</summary>
    [Fact]
    public async Task OpeningAReport_FromTheDashboard_NeverInvokesTheScanExecutor()
    {
        var viewerService = new FakeGuiReportViewerService();
        var (main, executor, scanExecution) = Build(viewerService: viewerService);
        main.NavigateCommand.Execute(NavigationPage.Scan);
        scanExecution.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => scanExecution.IsFinished));
        scanExecution.ViewResultsCommand.Execute(null);

        var dashboard = Assert.IsType<ResultsDashboardViewModel>(main.CurrentPageViewModel);
        dashboard.OpenReportCommand.Execute(null);

        Assert.Single(viewerService.Calls);
        Assert.Single(executor.Calls);
    }

    [Fact]
    public void ResultsPage_BeforeAnyScanHasEverCompleted_StillShowsThePlaceholder()
    {
        var (main, _, _) = Build();

        main.NavigateCommand.Execute(NavigationPage.Results);

        Assert.IsType<PlaceholderPageViewModel>(main.CurrentPageViewModel);
    }

    [Fact]
    public async Task ViewResultsCommand_CanExecute_OnlyOnceAScanHasFinished()
    {
        var (_, _, scanExecution) = Build();
        Assert.False(scanExecution.ViewResultsCommand.CanExecute(null));

        scanExecution.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => scanExecution.IsFinished));

        Assert.True(scanExecution.ViewResultsCommand.CanExecute(null));
    }
}
