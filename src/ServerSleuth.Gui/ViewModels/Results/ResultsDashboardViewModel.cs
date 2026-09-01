using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Verification;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.ViewModels.Results;

/// <summary>
/// GUI-4's Results Dashboard ViewModel — a pure PRESENTATION/INSPECTION layer over an already-
/// completed <see cref="ScanExecutionState"/>. Built exactly once per completed scan (by
/// <see cref="MainViewModel"/>, when <see cref="ScanExecutionViewModel.ViewResultsRequested"/>
/// fires) and held for the lifetime of that result — navigating between dashboard sections,
/// selecting an application, filtering, or returning to this page never reconstructs it and
/// never touches <c>IDiscoveryEngine</c>/<c>ScanPipelineRunner</c>/any risk or migration engine
/// (mechanically verified alongside <c>ScanExecutionViewModel</c>/<c>ScanConfigurationViewModel</c>
/// by the extended <c>NoScanExecutionFromGuiTests</c>).
///
/// Every collection below is either the exact <c>IReadOnlyList</c> instance the pipeline already
/// produced, or a thin, eagerly-built, one-way projection of it (<see cref="ApplicationRowViewModel"/>)
/// — never a recomputation of severity, migration status, coverage, or a numeric score (skill.md
/// GUI-4 §5, §17: "do not calculate a new risk score," "do not invent a 0-100 score anywhere").
/// </summary>
public sealed class ResultsDashboardViewModel : ObservableObject, IPageViewModel
{
    private readonly IGuiReportExportService? _exportService;
    private readonly IGuiReportViewerService? _viewerService;

    /// <summary>GUI-5 §1-2: both new dependencies are OPTIONAL (default <c>null</c>) — every
    /// GUI-4 call site that built this ViewModel from just a <see cref="ScanExecutionState"/>
    /// keeps compiling unchanged, and a dashboard built without them simply has Export/Open
    /// disabled (<see cref="ExportReportCommand"/>/<see cref="OpenReportCommand"/>'s own
    /// <c>CanExecute</c>) rather than throwing. <c>MainViewModel</c>'s real, DI-composed instance
    /// always supplies both.</summary>
    public ResultsDashboardViewModel(
        ScanExecutionState state, IGuiReportExportService? exportService = null, IGuiReportViewerService? viewerService = null)
    {
        State = state;
        _exportService = exportService;
        _viewerService = viewerService;

        var pipeline = state.PipelineResult;
        var report = pipeline?.Report;
        var serverRisk = pipeline?.Aggregation.Server;

        // GUI-7B: the join itself now lives in ApplicationRowViewModel.BuildFrom (shared with
        // MigrationOverviewViewModel) — this constructor still never sorts/recomputes anything.
        Applications = ApplicationRowViewModel.BuildFrom(pipeline);
        FilteredApplications = Applications;

        Report = report;
        ServerRiskSummary = serverRisk;

        // GUI-6A: the Discovery Inventory — built once from the exact same ScanPipelineResult,
        // never a second pipeline run. Null pipeline (Cancelled/Failed before analysis) degrades
        // to an empty inventory, exactly like every other section on this dashboard.
        Inventory = new InventoryExplorerViewModel(pipeline, state.Status);

        SelectApplicationCommand = new RelayCommand(parameter =>
        {
            if (parameter is ApplicationRowViewModel row)
            {
                SelectedApplication = row;
            }
        });

        _exportDirectory = state.OutputDirectory;
        _selectedReportFileName = ReportFileNames.FirstOrDefault() ?? string.Empty;

        // GUI-5 §3: "New Scan" reachable from the dashboard itself, not only from the Scan
        // Execution page's own completion buttons — raised, never handled here (MainViewModel
        // routes it through the SAME existing NavigationService/ScanExecutionViewModel flow, no
        // second navigation system).
        NewScanCommand = new RelayCommand(_ => NewScanRequested?.Invoke(this, EventArgs.Empty));

        ExportReportCommand = new RelayCommand(
            _ => ExecuteExport(),
            _ => _exportService is not null && State.PipelineResult is not null && !string.IsNullOrWhiteSpace(ExportDirectory));

        OpenReportCommand = new RelayCommand(
            _ => ExecuteOpenReport(),
            _ => _viewerService is not null && !string.IsNullOrWhiteSpace(SelectedReportFileName));
    }

    // ----- GUI-5 §3: dashboard-level actions (View Application already exists via
    // SelectApplicationCommand above) -----

    /// <summary>Raised by <see cref="NewScanCommand"/> — <see cref="MainViewModel"/> handles it
    /// exactly like <see cref="ScanExecutionViewModel.ReturnToConfigurationRequested"/> (same
    /// existing navigation flow, never a second one).</summary>
    public event EventHandler? NewScanRequested;

    public RelayCommand NewScanCommand { get; }

    // ----- GUI-5 §1, §4: Report Export — invokes the existing ReportArtifactFactory/
    // IReportExporter through IGuiReportExportService only; never File.WriteAllText/
    // WriteAllBytes/StreamWriter here, and never fabricates success. -----

    public static IReadOnlyList<ScanOutputFormat> ExportFormatOptions { get; } = Enum.GetValues<ScanOutputFormat>();

    public static IReadOnlyList<ScanOverwritePolicy> OverwritePolicyOptions { get; } = Enum.GetValues<ScanOverwritePolicy>();

    private ScanOutputFormat _exportFormat = ScanOutputFormat.Both;
    public ScanOutputFormat ExportFormat
    {
        get => _exportFormat;
        set => SetProperty(ref _exportFormat, value);
    }

    private ScanOverwritePolicy _exportOverwritePolicy = ScanOverwritePolicy.FailIfExists;
    public ScanOverwritePolicy ExportOverwritePolicy
    {
        get => _exportOverwritePolicy;
        set => SetProperty(ref _exportOverwritePolicy, value);
    }

    private string _exportDirectory;

    /// <summary>Defaults to the SAME directory the completed scan already wrote its reports to
    /// (<see cref="ScanExecutionState.OutputDirectory"/>) — freely editable so the user can
    /// export a second copy elsewhere, but never silently used to overwrite Scan Configuration's
    /// own recorded output directory.</summary>
    public string ExportDirectory
    {
        get => _exportDirectory;
        set => SetProperty(ref _exportDirectory, value ?? string.Empty);
    }

    private GuiReportExportResult? _lastExportResult;
    public GuiReportExportResult? LastExportResult
    {
        get => _lastExportResult;
        private set
        {
            if (SetProperty(ref _lastExportResult, value))
            {
                OnPropertyChanged(nameof(LastExportedFileNamesText));
            }
        }
    }

    /// <summary>A plain, comma-joined display string for <see cref="GuiReportExportResult.WrittenFileNames"/>
    /// — exists purely so the view can bind a <c>Run</c>'s <c>Text</c> to something other than a
    /// collection's own <c>ToString()</c>; never recomputes or reorders the underlying list.</summary>
    public string LastExportedFileNamesText => string.Join(", ", LastExportResult?.WrittenFileNames ?? []);

    public RelayCommand ExportReportCommand { get; }

    private void ExecuteExport()
    {
        if (_exportService is null || State.PipelineResult is null)
        {
            return;
        }

        // skill.md GUI-5 §4: never fabricated — whatever IGuiReportExportService itself reports
        // (success or a specific failure reason) is exactly what gets bound here.
        LastExportResult = _exportService.Export(State.PipelineResult, ExportDirectory, ExportFormat, ExportOverwritePolicy);
    }

    // ----- GUI-5 §2: Report Viewer — reads an already-written report file's raw text; never
    // regenerates the report and never triggers a new scan. -----

    private string _selectedReportFileName;
    public string SelectedReportFileName
    {
        get => _selectedReportFileName;
        set => SetProperty(ref _selectedReportFileName, value ?? string.Empty);
    }

    private GuiReportViewResult? _reportViewResult;
    public GuiReportViewResult? ReportViewResult
    {
        get => _reportViewResult;
        private set => SetProperty(ref _reportViewResult, value);
    }

    public RelayCommand OpenReportCommand { get; }

    private void ExecuteOpenReport()
    {
        if (_viewerService is null)
        {
            return;
        }

        ReportViewResult = _viewerService.ReadReportFile(State.OutputDirectory, SelectedReportFileName);
    }

    /// <summary>The completed scan's full non-sensitive state — Target/Platform/Status/
    /// Started/Finished/EntityCount/ErrorCount/ScannerStatuses/OutputPaths/PipelineResult.</summary>
    public ScanExecutionState State { get; }

    public NavigationPage Page => NavigationPage.Results;

    /// <summary>Null when the scan never reached a consolidated report (e.g.
    /// <see cref="ScanExecutionStatus.Cancelled"/>/<see cref="ScanExecutionStatus.Failed"/>
    /// before analysis completed) — every section below degrades to an explicit "no data"
    /// empty state rather than fabricating one (skill.md GUI-4 §19).</summary>
    public ServerMigrationAssessmentReport? Report { get; }

    public ServerRiskSummary? ServerRiskSummary { get; }

    public bool HasResults => Report is not null;

    /// <summary>Pure negation of <see cref="HasResults"/> — exists only because WPF's built-in
    /// <see cref="System.Windows.BooleanToVisibilityConverter"/> has no "invert" mode; kept as an
    /// explicit property (not a converter parameter trick) so the "no results yet" empty-state
    /// text's visibility rule is as obvious in the ViewModel as every other section's.</summary>
    public bool HasNoResults => !HasResults;

    // ----- SCAN SUMMARY (skill.md GUI-4 §5) -----
    public ScanExecutionStatus Status => State.Status;
    public string TargetDisplayName => State.TargetDisplayName;
    public TargetPlatform TargetPlatform => State.TargetPlatform;
    public DateTimeOffset? StartedAt => State.StartedAt;
    public DateTimeOffset? FinishedAt => State.FinishedAt;
    public TimeSpan? Duration => StartedAt.HasValue && FinishedAt.HasValue ? FinishedAt.Value - StartedAt.Value : null;
    public int EntityCount => State.EntityCount;
    public int ErrorCount => State.ErrorCount;
    public AssessmentCoverage? Coverage => Report?.Coverage;
    public IReadOnlyList<CoverageWarning> CoverageWarnings => Report?.CoverageWarnings ?? [];

    // ----- RISK SUMMARY (skill.md GUI-4 §5) — actual RiskAggregator output only, never a
    // recomputed score, never an invented "None" tier beyond the enum's own None. -----
    public AggregateSeverity OverallRiskSeverity => ServerRiskSummary?.OverallSeverity ?? AggregateSeverity.None;
    public int CriticalCount => ServerRiskSummary?.CriticalCount ?? 0;
    public int HighCount => ServerRiskSummary?.HighCount ?? 0;
    public int MediumCount => ServerRiskSummary?.MediumCount ?? 0;
    public int LowCount => ServerRiskSummary?.LowCount ?? 0;
    public int InfoCount => ServerRiskSummary?.InfoCount ?? 0;
    public int TotalFindingCount => ServerRiskSummary?.TotalFindingCount ?? 0;

    /// <summary>Phase 7B's own deterministic ordering (Severity desc, Impact desc, Confidence
    /// desc, RuleId/FindingId ordinal) — never re-ranked here (skill.md GUI-4 §8).</summary>
    public IReadOnlyList<RiskFinding> TopRisks => ServerRiskSummary?.TopRisks ?? [];

    public IReadOnlyList<RiskFinding> AllFindings => ServerRiskSummary?.Findings ?? [];

    // ----- MIGRATION SUMMARY (skill.md GUI-4 §5) -----
    public MigrationStatus? OverallMigrationStatus => Report?.ServerSummary.OverallMigrationStatus;
    public int ApplicationCount => Report?.ServerSummary.ApplicationCount ?? 0;
    public int BlockedApplicationCount => Report?.ServerSummary.BlockedApplicationCount ?? 0;
    public int NeedsRemediationApplicationCount => Report?.ServerSummary.NeedsRemediationApplicationCount ?? 0;
    public int ReadyWithConditionsApplicationCount => Report?.ServerSummary.ReadyWithConditionsApplicationCount ?? 0;
    public int ReadyApplicationCount => Report?.ServerSummary.ReadyApplicationCount ?? 0;
    public int ActionCount => Report?.ServerSummary.ActionCount ?? 0;
    public int VerificationCheckCount => Report?.ServerSummary.VerificationCheckCount ?? 0;
    public int DependencyCount => Report?.ServerSummary.DependencyCount ?? 0;

    // ----- DISCOVERY INVENTORY (skill.md GUI-6A) -----
    public InventoryExplorerViewModel Inventory { get; }

    // ----- APPLICATIONS (skill.md GUI-4 §6) -----
    public IReadOnlyList<ApplicationRowViewModel> Applications { get; }

    // ----- MIGRATION ISSUES (skill.md GUI-4 §9) — every issue in scope, app- and server-scoped
    // alike, exactly as ServerMigrationAssessment.Issues (Phase 8A) already enumerates it,
    // ordinal-sorted by IssueId. -----
    public IReadOnlyList<MigrationIssue> AllMigrationIssues => Report?.Assessment.Server.Issues ?? [];
    public IReadOnlyList<MigrationIssue> ServerLevelIssues => Report?.ServerLevelIssues ?? [];

    // ----- MIGRATION ACTIONS (skill.md GUI-4 §10) — declarative only; no Execute affordance
    // exists anywhere in this ViewModel or its View. -----
    public IReadOnlyList<MigrationAction> Actions => Report?.Actions ?? [];

    // ----- VERIFICATION CHECKS (skill.md GUI-4 §11) — declarative only; no Run affordance. -----
    public IReadOnlyList<MigrationVerificationCheck> PreMigrationChecks => Report?.PreMigrationChecks ?? [];
    public IReadOnlyList<MigrationVerificationCheck> PostMigrationChecks => Report?.PostMigrationChecks ?? [];

    // ----- DEPENDENCIES (skill.md GUI-4 §12) — grouped exactly as
    // ServerMigrationAssessmentReport.Dependencies (Phase 8C) already groups them; never
    // regrouped/reclassified from strings here. -----
    public IReadOnlyList<MigrationDependencyGroup> DependencyGroups => Report?.Dependencies ?? [];
    public IReadOnlyList<MigrationDependency> SharedInfrastructure => Report?.SharedInfrastructure ?? [];

    // ----- SCANNER STATUS (skill.md GUI-4 §13) — reused verbatim from GUI-3. -----
    public IReadOnlyList<ScannerProgressInfo> ScannerStatuses => State.ScannerStatuses;

    // ----- REPORT ARTIFACTS (skill.md GUI-4 §15) -----
    public IReadOnlyList<string> ReportFileNames => State.OutputPaths;

    // ----- FILTERING/SORTING (skill.md GUI-4 §18: presentation-only, never mutates Applications) -----

    /// <summary>The severity-filter ComboBox's own item source — <c>null</c> (bound to the
    /// literal text "All") plus every real <see cref="AggregateSeverity"/> member, in the enum's
    /// own declared ascending-severity order. A plain static array, never a
    /// Dictionary/HashSet-ordered collection.</summary>
    public static IReadOnlyList<AggregateSeverity?> SeverityFilterOptions { get; } =
        new AggregateSeverity?[] { null }.Concat(Enum.GetValues<AggregateSeverity>().Cast<AggregateSeverity?>()).ToList();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                ApplyApplicationFilter();
            }
        }
    }

    private AggregateSeverity? _severityFilter;

    /// <summary>Null means "no severity filter applied" (show every severity).</summary>
    public AggregateSeverity? SeverityFilter
    {
        get => _severityFilter;
        set
        {
            if (SetProperty(ref _severityFilter, value))
            {
                ApplyApplicationFilter();
            }
        }
    }

    private bool _onlyWithIssues;
    public bool OnlyWithIssues
    {
        get => _onlyWithIssues;
        set
        {
            if (SetProperty(ref _onlyWithIssues, value))
            {
                ApplyApplicationFilter();
            }
        }
    }

    private IReadOnlyList<ApplicationRowViewModel> _filteredApplications = [];
    public IReadOnlyList<ApplicationRowViewModel> FilteredApplications
    {
        get => _filteredApplications;
        private set => SetProperty(ref _filteredApplications, value);
    }

    private void ApplyApplicationFilter()
    {
        IEnumerable<ApplicationRowViewModel> query = Applications;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(a => a.ApplicationName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (SeverityFilter is { } severity)
        {
            query = query.Where(a => a.RiskSeverity == severity);
        }

        if (OnlyWithIssues)
        {
            query = query.Where(a => a.IssueCount > 0);
        }

        // A fresh list every time — never reorders/mutates the master Applications collection,
        // and the master list itself is untouched regardless of how many times filters change.
        FilteredApplications = query.ToList();
    }

    // ----- APPLICATION DETAIL (skill.md GUI-4 §7) -----
    private ApplicationRowViewModel? _selectedApplication;
    public ApplicationRowViewModel? SelectedApplication
    {
        get => _selectedApplication;
        set
        {
            if (SetProperty(ref _selectedApplication, value))
            {
                OnPropertyChanged(nameof(SelectedApplicationDetail));
            }
        }
    }

    public ApplicationDetailViewModel? SelectedApplicationDetail => SelectedApplication?.Detail;

    public RelayCommand SelectApplicationCommand { get; }
}
