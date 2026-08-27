using System.Collections.ObjectModel;
using System.Reflection;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.ViewModels;

/// <summary>
/// The application shell's ViewModel — owns the navigation item list, the currently-displayed
/// placeholder page, and the status/footer text bound from <see cref="IApplicationStateService"/>.
/// GUI-1 §Objective: this type proves the shell/navigation architecture; it does not orchestrate
/// any real scan/discovery/analysis/reporting work (skill.md GUI-1's own explicit exclusion —
/// none of those pipeline stages are referenced anywhere in this class, mechanically verified by
/// <c>NoDuplicatePipelineEngineTests</c>/<c>NoDirectPlatformAccessTests</c>).
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    /// <summary>The deterministic, ordered page list GUI-1 §4/§9 requires — a plain array, never
    /// a <c>Dictionary</c>/<c>HashSet</c> whose enumeration order is not contractually
    /// guaranteed.</summary>
    private static readonly (NavigationPage Page, string Label, string Description)[] Pages =
    [
        (NavigationPage.Dashboard, "Dashboard", "An overview of the current target and its most recent scan will appear here."),
        (NavigationPage.Scan, "Scan", "Scan configuration and target selection will appear here."),
        (NavigationPage.Results, "Results", "Discovered entities and their relationships will appear here."),
        (NavigationPage.Migration, "Migration", "Risk findings and the migration assessment will appear here."),
        (NavigationPage.Reports, "Reports", "Generated reports and export options will appear here."),
        (NavigationPage.Settings, "Settings", "Application preferences will appear here.")
    ];

    private readonly INavigationService _navigationService;
    private readonly IApplicationStateService _applicationStateService;
    private readonly ScanConfigurationViewModel _scanConfigurationViewModel;
    private readonly ScanExecutionViewModel _scanExecutionViewModel;

    /// <summary>GUI-5 §1-2: optional (default <c>null</c>) purely so the two existing
    /// two-call-site test constructors keep compiling unchanged — <c>CompositionRoot</c>'s real,
    /// DI-composed instance always supplies both. Passed straight through to every
    /// <see cref="ResultsDashboardViewModel"/> this class builds; never used directly here (this
    /// class itself still orchestrates nothing beyond navigation, per its own doc comment).</summary>
    private readonly IGuiReportExportService? _reportExportService;
    private readonly IGuiReportViewerService? _reportViewerService;

    /// <summary>GUI-3 §Step12: whether the Scan page currently shows execution (rather than
    /// configuration) — set the moment <see cref="ScanConfigurationViewModel.ScanRequested"/>
    /// fires, cleared when <see cref="ScanExecutionViewModel.ReturnToConfigurationRequested"/>
    /// fires. A plain <c>bool</c>, not a third navigation page — skill.md's own "Scan
    /// Configuration → Start Scan → Scan Execution → Completed" flow stays under the SAME
    /// <see cref="NavigationPage.Scan"/> nav item throughout, never a separate one.</summary>
    private bool _showScanExecution;

    /// <summary>GUI-4 §Step4/§Step5: the currently-built Results Dashboard, if any — null until
    /// the user has clicked "View Results" at least once. Built exactly once per completed scan
    /// (see the <c>ViewResultsRequested</c> handler below) and reused for every subsequent
    /// navigation back to <see cref="NavigationPage.Results"/>; never rebuilt merely because the
    /// user switches away and back (GUI-4's own central "no second pipeline execution,
    /// no rebuild-on-navigate" rule).</summary>
    private ResultsDashboardViewModel? _resultsDashboardViewModel;

    public MainViewModel(
        INavigationService navigationService, IApplicationStateService applicationStateService,
        ScanConfigurationViewModel scanConfigurationViewModel, ScanExecutionViewModel scanExecutionViewModel,
        IGuiReportExportService? reportExportService = null, IGuiReportViewerService? reportViewerService = null)
    {
        _navigationService = navigationService;
        _applicationStateService = applicationStateService;
        _scanConfigurationViewModel = scanConfigurationViewModel;
        _scanExecutionViewModel = scanExecutionViewModel;
        _reportExportService = reportExportService;
        _reportViewerService = reportViewerService;

        NavigationItems = new ObservableCollection<NavigationItemViewModel>(
            Pages.Select(p => new NavigationItemViewModel(p.Page, p.Label)));

        NavigateCommand = new RelayCommand(parameter =>
        {
            if (parameter is NavigationPage page)
            {
                _navigationService.NavigateTo(page);
            }
        });

        _navigationService.CurrentPageChanged += (_, page) => ApplyCurrentPage(page);
        _applicationStateService.StateChanged += (_, _) => RefreshStatusText();

        _scanConfigurationViewModel.ScanRequested += (_, args) =>
        {
            _showScanExecution = true;
            _scanExecutionViewModel.Start(args.Request, args.Credentials);
            ApplyCurrentPage(_navigationService.CurrentPage);
        };
        _scanExecutionViewModel.ReturnToConfigurationRequested += (_, _) => GoToScanConfiguration();
        _scanExecutionViewModel.ViewResultsRequested += (_, _) =>
        {
            // GUI-4 §Step2-3: built purely from the ScanExecutionViewModel's own already-completed
            // State — no executor, no engine, no re-fetch. A fresh instance per "View Results"
            // click (so a later, different completed scan gets its own dashboard), but never
            // rebuilt just because the user navigates away and back to NavigationPage.Results.
            var dashboard = new ResultsDashboardViewModel(_scanExecutionViewModel.State, _reportExportService, _reportViewerService);
            dashboard.NewScanRequested += (_, _) => GoToScanConfiguration();
            _resultsDashboardViewModel = dashboard;
            _navigationService.NavigateTo(NavigationPage.Results);
            ApplyCurrentPage(_navigationService.CurrentPage);
        };

        ApplyCurrentPage(_navigationService.CurrentPage);
        RefreshStatusText();
    }

    public string ApplicationTitle => "ServerSleuth";

    public string VersionText { get; } = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0"}";

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public RelayCommand NavigateCommand { get; }

    private IPageViewModel _currentPageViewModel = null!;

    /// <summary>GUI-2: widened from <c>PlaceholderPageViewModel</c> to the common
    /// <see cref="IPageViewModel"/> shape — the Scan page now shows a real
    /// <see cref="ScanConfigurationViewModel"/>; the other five pages remain placeholders.</summary>
    public IPageViewModel CurrentPageViewModel
    {
        get => _currentPageViewModel;
        private set => SetProperty(ref _currentPageViewModel, value);
    }

    private string _statusText = string.Empty;

    /// <summary>Concise, user-safe status text only — never a raw exception message beyond what
    /// the error boundary already sanitized, and never target credential material (there is
    /// none to leak: <see cref="ServerSleuth.Core.Targets.ScanTarget"/> carries no credential
    /// field, skill.md GUI-1 §5/§12).</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>The one "New Scan" handler both <see cref="ScanExecutionViewModel.ReturnToConfigurationRequested"/>
    /// (GUI-3, from the Scan Execution page) and <see cref="ResultsDashboardViewModel.NewScanRequested"/>
    /// (GUI-5 §3, from the Results Dashboard) route through — the SAME existing
    /// <see cref="NavigationService"/>/Scan-page-slot mechanism GUI-3 already established, never a
    /// second navigation system.</summary>
    private void GoToScanConfiguration()
    {
        _showScanExecution = false;
        _navigationService.NavigateTo(NavigationPage.Scan);
        ApplyCurrentPage(_navigationService.CurrentPage);
    }

    private void ApplyCurrentPage(NavigationPage page)
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = item.Page == page;
        }

        if (page == NavigationPage.Scan)
        {
            CurrentPageViewModel = _showScanExecution ? _scanExecutionViewModel : _scanConfigurationViewModel;
            return;
        }

        // GUI-4 §Step4: shows the real dashboard once "View Results" has been clicked at least
        // once; before that (or after a new session with no completed scan yet) the Results nav
        // item still shows the original GUI-1 placeholder — never a fabricated empty dashboard
        // that looks like a real, still-loading result.
        if (page == NavigationPage.Results && _resultsDashboardViewModel is not null)
        {
            CurrentPageViewModel = _resultsDashboardViewModel;
            return;
        }

        var (_, label, description) = Pages.Single(p => p.Page == page);
        CurrentPageViewModel = new PlaceholderPageViewModel(page, label, description);
    }

    private void RefreshStatusText()
    {
        var state = _applicationStateService.Current;
        var targetText = state.Target?.DisplayName ?? state.Target?.Id ?? "No target selected";
        var scanText = state.IsScanRunning ? "Scanning…" : "Idle";
        StatusText = state.LastErrorMessage is { Length: > 0 }
            ? $"Error: {state.LastErrorMessage}"
            : $"{targetText} — {scanText}";
    }
}
