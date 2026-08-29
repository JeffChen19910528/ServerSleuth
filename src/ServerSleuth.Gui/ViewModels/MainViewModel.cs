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
    /// guaranteed. GUI-7: label/description are now resource KEYS (resolved through
    /// <see cref="_languageService"/> at the point of use), not literal English text.</summary>
    private static readonly (NavigationPage Page, string LabelKey, string DescriptionKey)[] Pages =
    [
        (NavigationPage.Dashboard, "Nav.Dashboard.Label", "Nav.Dashboard.Description"),
        (NavigationPage.Scan, "Nav.Scan.Label", "Nav.Scan.Description"),
        (NavigationPage.Inventory, "Nav.Inventory.Label", "Nav.Inventory.Description"),
        (NavigationPage.Results, "Nav.Results.Label", "Nav.Results.Description"),
        (NavigationPage.Migration, "Nav.Migration.Label", "Nav.Migration.Description"),
        (NavigationPage.Reports, "Nav.Reports.Label", "Nav.Reports.Description"),
        (NavigationPage.Settings, "Nav.Settings.Label", "Nav.Settings.Description")
    ];

    private readonly INavigationService _navigationService;
    private readonly IApplicationStateService _applicationStateService;
    private readonly ILanguageService _languageService;
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
        IGuiReportExportService? reportExportService = null, IGuiReportViewerService? reportViewerService = null,
        ILanguageService? languageService = null)
    {
        _navigationService = navigationService;
        _applicationStateService = applicationStateService;
        _scanConfigurationViewModel = scanConfigurationViewModel;
        _scanExecutionViewModel = scanExecutionViewModel;
        _reportExportService = reportExportService;
        _reportViewerService = reportViewerService;
        _languageService = languageService ?? new LanguageService();

        NavigationItems = new ObservableCollection<NavigationItemViewModel>(
            Pages.Select(p => new NavigationItemViewModel(p.Page, _languageService.T(p.LabelKey))));

        NavigateCommand = new RelayCommand(parameter =>
        {
            if (parameter is NavigationPage page)
            {
                _navigationService.NavigateTo(page);
            }
        });

        SetLanguageCommand = new RelayCommand(parameter =>
        {
            if (parameter is GuiLanguage language)
            {
                _languageService.SetLanguage(language);
            }
        });

        _navigationService.CurrentPageChanged += (_, page) => ApplyCurrentPage(page);
        _applicationStateService.StateChanged += (_, _) => RefreshStatusText();
        _languageService.LanguageChanged += (_, _) => ApplyLanguageChange();

        _scanConfigurationViewModel.ScanRequested += (_, args) =>
        {
            _showScanExecution = true;
            _scanExecutionViewModel.Start(args.Request, args.Credentials);
            ApplyCurrentPage(_navigationService.CurrentPage);
        };
        _scanExecutionViewModel.ReturnToConfigurationRequested += (_, _) => GoToScanConfiguration();
        _scanExecutionViewModel.ViewResultsRequested += (_, _) => ShowResults();

        ApplyCurrentPage(_navigationService.CurrentPage);
        RefreshStatusText();
    }

    public string ApplicationTitle => "ServerSleuth";

    public string VersionText { get; } = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0"}";

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public RelayCommand NavigateCommand { get; }

    /// <summary>GUI-7: takes a <see cref="GuiLanguage"/> as its command parameter — bound from
    /// the two language-toggle buttons in <c>MainWindow</c>'s header.</summary>
    public RelayCommand SetLanguageCommand { get; }

    public GuiLanguage CurrentLanguage => _languageService.CurrentLanguage;

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

    /// <summary>GUI-4 §Step2-3, now also reachable from GUI-7A's Dashboard "View Results" button
    /// (not only <see cref="ScanExecutionViewModel.ViewResultsRequested"/>): built purely from
    /// the <see cref="ScanExecutionViewModel"/>'s own already-completed <c>State</c> — no
    /// executor, no engine, no re-fetch. A fresh instance every time this is CALLED (so a later,
    /// different completed scan gets its own dashboard), but never rebuilt merely because the
    /// user navigates away and back to <see cref="NavigationPage.Results"/>.</summary>
    private void ShowResults()
    {
        var dashboard = new ResultsDashboardViewModel(_scanExecutionViewModel.State, _reportExportService, _reportViewerService);
        dashboard.NewScanRequested += (_, _) => GoToScanConfiguration();
        _resultsDashboardViewModel = dashboard;
        _navigationService.NavigateTo(NavigationPage.Results);
        ApplyCurrentPage(_navigationService.CurrentPage);
    }

    /// <summary>GUI-7A: the Dashboard is rebuilt fresh on every visit (unlike Results/Inventory
    /// selection state, it holds none of its own) directly from
    /// <see cref="ScanExecutionViewModel.State"/> — the exact same source
    /// <see cref="ResultsDashboardViewModel"/>/<see cref="InventoryExplorerViewModel"/> already
    /// read, never a second pipeline execution or a separate "latest result" store.</summary>
    private DashboardOverviewViewModel BuildDashboardOverviewViewModel()
    {
        var dashboard = new DashboardOverviewViewModel(_scanExecutionViewModel.State);
        dashboard.StartScanRequested += (_, _) => GoToScanConfiguration();
        dashboard.ViewResultsRequested += (_, _) => ShowResults();
        dashboard.ViewInventoryRequested += (_, _) =>
        {
            _navigationService.NavigateTo(NavigationPage.Inventory);
            ApplyCurrentPage(_navigationService.CurrentPage);
        };
        return dashboard;
    }

    /// <summary>GUI-7B: same "rebuild fresh on every visit" contract as Dashboard/Inventory — the
    /// selected-application detail is presentation-only state, not worth the complexity of a
    /// separate cache-invalidation rule for a page reachable independently of "View Results."</summary>
    private MigrationOverviewViewModel BuildMigrationOverviewViewModel()
    {
        var migration = new MigrationOverviewViewModel(_scanExecutionViewModel.State);
        migration.StartScanRequested += (_, _) => GoToScanConfiguration();
        return migration;
    }

    /// <summary>GUI-7B: same "rebuild fresh on every visit" contract as Dashboard/Inventory — a
    /// stale export/viewer result from a previous visit is never shown again on return.</summary>
    private ReportsOverviewViewModel BuildReportsOverviewViewModel()
    {
        var reports = new ReportsOverviewViewModel(_scanExecutionViewModel.State, _reportExportService, _reportViewerService);
        reports.StartScanRequested += (_, _) => GoToScanConfiguration();
        return reports;
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

        if (page == NavigationPage.Dashboard)
        {
            CurrentPageViewModel = BuildDashboardOverviewViewModel();
            return;
        }

        // GUI-7A: InventoryExplorerViewModel already degrades to an explicit empty state
        // (skill.md GUI-6A §14) with no completed scan — reachable at any time, never a
        // placeholder, and never a second inventory engine.
        if (page == NavigationPage.Inventory)
        {
            CurrentPageViewModel = new InventoryExplorerViewModel(_scanExecutionViewModel.State.PipelineResult, _scanExecutionViewModel.State.Status);
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

        if (page == NavigationPage.Migration)
        {
            CurrentPageViewModel = BuildMigrationOverviewViewModel();
            return;
        }

        if (page == NavigationPage.Reports)
        {
            CurrentPageViewModel = BuildReportsOverviewViewModel();
            return;
        }

        if (page == NavigationPage.Settings)
        {
            CurrentPageViewModel = new SettingsViewModel(_scanConfigurationViewModel, _languageService);
            return;
        }

        var (_, labelKey, descriptionKey) = Pages.Single(p => p.Page == page);
        CurrentPageViewModel = new PlaceholderPageViewModel(page, _languageService.T(labelKey), _languageService.T(descriptionKey));
    }

    private void RefreshStatusText()
    {
        var state = _applicationStateService.Current;
        var targetText = state.Target?.DisplayName ?? state.Target?.Id ?? _languageService.T("Status.NoTargetSelected");
        var scanText = state.IsScanRunning ? _languageService.T("Status.Scanning") : _languageService.T("Status.Idle");
        StatusText = state.LastErrorMessage is { Length: > 0 }
            ? $"{_languageService.T("Status.ErrorPrefix")}{state.LastErrorMessage}"
            : $"{targetText} — {scanText}";
    }

    /// <summary>GUI-7: re-resolves every ViewModel-owned (i.e. not a plain XAML
    /// <c>{DynamicResource}</c>) string against the new language — nav item labels, the
    /// currently-shown placeholder page (if any), and the status footer. Never rebuilds
    /// <see cref="ScanConfigurationViewModel"/>/<see cref="ScanExecutionViewModel"/>/the results
    /// dashboard themselves, so no in-progress scan/results state is lost.</summary>
    private void ApplyLanguageChange()
    {
        OnPropertyChanged(nameof(CurrentLanguage));

        foreach (var item in NavigationItems)
        {
            var (_, labelKey, _) = Pages.Single(p => p.Page == item.Page);
            item.Label = _languageService.T(labelKey);
        }

        ApplyCurrentPage(_navigationService.CurrentPage);
        RefreshStatusText();
    }
}
