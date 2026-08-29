using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Composition;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels;
using ServerSleuth.Gui.ViewModels.Results;
using ServerSleuth.Gui.Views;
using GuiApp = ServerSleuth.Gui.App.App;

// Deliberately real: a real Application, a real MainWindow, a real composition root, a real
// layout pass, under this project's own InvariantGlobalization=true (matching the shipped
// ServerSleuth.Gui.exe exactly) — see the .csproj comment for why an in-process xUnit test cannot
// exercise this. Exit code 0 = every scenario below completed with no unhandled exception and no
// LastErrorMessage ever set; any other exit code means a regression, with details on stderr for
// RealWindowRuntimeConfigTests (in ServerSleuth.Gui.Tests) to surface in its assertion failure.

var failures = new List<string>();

var thread = new Thread(() => Run(failures));
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();

if (failures.Count > 0)
{
    foreach (var failure in failures)
    {
        Console.Error.WriteLine(failure);
    }

    return 1;
}

Console.WriteLine("OK — every page, with and without a language switch, produced no unhandled exception.");
return 0;

static void Run(List<string> failures)
{
    var app = new GuiApp();
    app.InitializeComponent();

    // Default ShutdownMode is OnLastWindowClose — this harness opens and closes several windows
    // in sequence on the SAME Application, and letting the app shut down after the first one's
    // Close() leaves every later Window.Show() running against an already-shutting-down
    // Application (observed as a spurious "Cannot find non-neutral culture related to 'en-us'"
    // from BindingExpression.GetCulture(), unrelated to anything under test).
    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

    Exception? dispatcherException = null;
    app.DispatcherUnhandledException += (_, e) =>
    {
        dispatcherException ??= e.Exception;
        e.Handled = true;
    };

    // Not every exception this scenario can throw necessarily routes through
    // DispatcherUnhandledException (a synchronous call on this thread, e.g. UpdateLayout()
    // itself, can throw directly) — catch here too so the failure is reported cleanly either way.
    try
    {
        RunScenario(failures, () => dispatcherException, ex => dispatcherException = ex, switchToTraditionalChinese: false);
    }
    catch (Exception ex)
    {
        failures.Add($"[without language switch] Threw directly (not via Dispatcher): {ex}");
    }

    try
    {
        RunScenario(failures, () => dispatcherException, ex => dispatcherException = ex, switchToTraditionalChinese: true);
    }
    catch (Exception ex)
    {
        failures.Add($"[with language switch] Threw directly (not via Dispatcher): {ex}");
    }

    try
    {
        RunResultsDashboardScenario(failures, () => dispatcherException, ex => dispatcherException = ex, switchToTraditionalChinese: false);
    }
    catch (Exception ex)
    {
        failures.Add($"[results dashboard, without language switch] Threw directly (not via Dispatcher): {ex}");
    }

    try
    {
        RunResultsDashboardScenario(failures, () => dispatcherException, ex => dispatcherException = ex, switchToTraditionalChinese: true);
    }
    catch (Exception ex)
    {
        failures.Add($"[results dashboard, with language switch] Threw directly (not via Dispatcher): {ex}");
    }

    app.Shutdown();
}

// Reproduces the crash reported against the real, shipped GUI: MainViewModel's own
// NavigationCommand walk (RunScenario above) never has a completed scan, so the Results
// dashboard it navigates to is always the empty/no-results placeholder — it never reaches the
// bindings inside the Migration Summary expander that threw InvalidOperationException in
// production (gui-*.log — see GuiExceptionHandler and ResultsDashboardView.xaml). This scenario
// drives the SAME real MainWindow/MainViewModel/ScanExecutionViewModel navigation flow
// MainViewModelResultsNavigationTests (ServerSleuth.Gui.Tests) exercises in-process — a fake
// IGuiScanExecutor stands in for the real pipeline (never touched here, matching every other
// scenario in this file) and returns a real, fully-populated ScanExecutionState
// (ScanResultFixtureFactory) — so the Results dashboard actually shown is byte-for-byte what a
// completed scan produces in the shipped app, not a synthetic stand-in. Then, since the failure
// is specifically tied to content that starts inside a collapsed Expander, it expands every
// Expander on the page (forcing the same layout-triggered binding attach the collapsed content
// only gets lazily/incidentally in the shipped app) both before and after a language switch,
// matching how the user actually hit it.
static void RunResultsDashboardScenario(List<string> failures, Func<Exception?> getDispatcherException, Action<Exception?> resetDispatcherException, bool switchToTraditionalChinese)
{
    resetDispatcherException(null);

    var navigation = new NavigationService();
    var applicationState = new ApplicationStateService();
    var languageService = new LanguageService();
    var scanConfiguration = new ScanConfigurationViewModel(new ScanConfigurationValidator(), new ScanRequestFactory());
    // A real DiscoveryEntity, not the factory's own null/empty default — otherwise the standalone
    // Inventory page (and its category-chip/item-selection bindings, exercised below) would have
    // nothing to actually render, exactly the gap that let InventoryExplorerView.xaml's own
    // Run-hosted bindings go untested against a real fresh-rebuild-plus-selection scenario.
    var discoveryEntity = new ServerSleuth.Core.Models.Service
    {
        Id = "service:harness", Name = "HarnessService", Type = "Service", Source = "ServiceControlManager",
        Status = ServerSleuth.Core.Enums.EntityStatus.Running, Confidence = ServerSleuth.Core.Evidence.Confidence.VeryHigh()
    };
    var completedState = ScanResultFixtureFactory.BuildCompletedState(
        new ScanResultFixtureFactory.Options { DiscoveryEntities = [discoveryEntity] });
    var executor = new HarnessFakeScanExecutor
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
    var mainViewModel = new MainViewModel(navigation, applicationState, scanConfiguration, scanExecution, languageService: languageService);
    var window = new MainWindow(mainViewModel);

    window.Show();
    window.UpdateLayout();
    PumpDispatcherOnce();

    mainViewModel.NavigateCommand.Execute(NavigationPage.Scan);
    scanExecution.Start(
        new ScanRequest
        {
            Target = ScanTarget.Local(TargetPlatform.Windows),
            OutputDirectory = "./out",
            OutputFormat = ScanOutputFormat.Both,
            OverwritePolicy = ScanOverwritePolicy.FailIfExists,
            Verbose = false
        },
        ScanCredentialInput.Empty);

    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
    while (!scanExecution.IsFinished && DateTime.UtcNow < deadline)
    {
        PumpDispatcherOnce();
    }

    scanExecution.ViewResultsCommand.Execute(null);
    window.UpdateLayout();
    PumpDispatcherOnce();

    var reachedResultsDashboard = mainViewModel.CurrentPageViewModel is ResultsDashboardViewModel;

    if (switchToTraditionalChinese)
    {
        languageService.SetLanguage(GuiLanguage.TraditionalChinese);
        window.UpdateLayout();
        PumpDispatcherOnce();
    }

    foreach (var expander in FindVisualChildren<Expander>(window))
    {
        expander.IsExpanded = true;
        window.UpdateLayout();
        PumpDispatcherOnce();
    }

    // GUI-7B/7C: also exercise the standalone Inventory page (select the first item, so
    // InventoryDetailView's binding actually attaches too), Migration (select the first
    // application, so the reused ApplicationDetailView's binding actually attaches — the generic
    // RunScenario walk above never has a completed scan, so it never reaches this), Reports (open
    // the first report file, so the raw-text viewer's binding actually attaches too), and
    // Settings, all with the SAME real completed scan, before/after the SAME language switch —
    // matching how the "TargetDisplayName" Run-hosted binding crash on Dashboard was originally
    // found (see DashboardView.xaml's own comment).
    mainViewModel.NavigateCommand.Execute(NavigationPage.Inventory);
    window.UpdateLayout();
    PumpDispatcherOnce();
    if (mainViewModel.CurrentPageViewModel is InventoryExplorerViewModel inventory && inventory.Items.Count > 0)
    {
        inventory.SelectedItem = inventory.Items[0];
        window.UpdateLayout();
        PumpDispatcherOnce();
    }

    mainViewModel.NavigateCommand.Execute(NavigationPage.Migration);
    window.UpdateLayout();
    PumpDispatcherOnce();
    if (mainViewModel.CurrentPageViewModel is MigrationOverviewViewModel migration && migration.Applications.Count > 0)
    {
        migration.SelectApplicationCommand.Execute(migration.Applications[0]);
        window.UpdateLayout();
        PumpDispatcherOnce();
    }

    mainViewModel.NavigateCommand.Execute(NavigationPage.Reports);
    window.UpdateLayout();
    PumpDispatcherOnce();
    if (mainViewModel.CurrentPageViewModel is ReportsOverviewViewModel reports && reports.ReportFileNames.Count > 0)
    {
        reports.SelectedReportFileName = reports.ReportFileNames[0];
        reports.OpenReportCommand.Execute(null);
        window.UpdateLayout();
        PumpDispatcherOnce();
    }

    mainViewModel.NavigateCommand.Execute(NavigationPage.Settings);
    window.UpdateLayout();
    PumpDispatcherOnce();

    window.Close();

    var scenario = switchToTraditionalChinese ? "results dashboard, with language switch" : "results dashboard, without language switch";

    if (!reachedResultsDashboard)
    {
        failures.Add($"[{scenario}] Navigation never reached the Results dashboard — scenario did not actually test anything.");
    }

    if (getDispatcherException() is { } exception)
    {
        failures.Add($"[{scenario}] DispatcherUnhandledException: {exception}");
    }

    if (applicationState.Current.LastErrorMessage is { } message)
    {
        failures.Add($"[{scenario}] LastErrorMessage was set: {message}");
    }
}

static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
{
    var childCount = VisualTreeHelper.GetChildrenCount(root);
    for (var i = 0; i < childCount; i++)
    {
        var child = VisualTreeHelper.GetChild(root, i);
        if (child is T typedChild)
        {
            yield return typedChild;
        }

        foreach (var grandchild in FindVisualChildren<T>(child))
        {
            yield return grandchild;
        }
    }
}

static void RunScenario(List<string> failures, Func<Exception?> getDispatcherException, Action<Exception?> resetDispatcherException, bool switchToTraditionalChinese)
{
    resetDispatcherException(null);

    using var services = (ServiceProvider)CompositionRoot.Build();
    var mainViewModel = services.GetRequiredService<MainViewModel>();
    var window = new MainWindow(mainViewModel);

    window.Show();
    window.UpdateLayout();
    PumpDispatcherOnce();

    if (switchToTraditionalChinese)
    {
        mainViewModel.SetLanguageCommand.Execute(GuiLanguage.TraditionalChinese);
        window.UpdateLayout();
        PumpDispatcherOnce();
    }

    foreach (var page in Enum.GetValues<NavigationPage>())
    {
        mainViewModel.NavigateCommand.Execute(page);
        window.UpdateLayout();
        PumpDispatcherOnce();
    }

    window.Close();

    var applicationState = services.GetRequiredService<IApplicationStateService>().Current;
    var scenario = switchToTraditionalChinese ? "with language switch" : "without language switch";

    if (getDispatcherException() is { } exception)
    {
        failures.Add($"[{scenario}] DispatcherUnhandledException: {exception}");
    }

    if (applicationState.LastErrorMessage is { } message)
    {
        failures.Add($"[{scenario}] LastErrorMessage was set: {message}");
    }
}

static void PumpDispatcherOnce()
{
    var frame = new DispatcherFrame();
    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
    Dispatcher.PushFrame(frame);
}

// A minimal stand-in for ServerSleuth.Gui.Tests.Fakes.FakeGuiScanExecutor (internal to that
// project, and referencing it here would need the same project either way) — the real pipeline is
// never touched; it hands back a caller-supplied ScanCompletionState.
sealed class HarnessFakeScanExecutor : IGuiScanExecutor
{
    public required ScanCompletionState CompletionToReturn { get; init; }

    public Task<ScanCompletionState> ExecuteAsync(
        ScanRequest request, ScanCredentialInput credentials, IProgress<ScanProgressState> progress, CancellationToken cancellationToken)
    {
        progress.Report(new ScanProgressState { Stage = ScanStage.Preparing });
        progress.Report(new ScanProgressState { Stage = ScanStage.Discovery, EntityCount = CompletionToReturn.EntityCount });
        return Task.FromResult(CompletionToReturn);
    }
}
