using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.ViewModels;

/// <summary>
/// GUI-7B's Reports page — a lightweight view over the latest completed scan's already-written
/// report files, reusing the EXACT SAME <see cref="IGuiReportExportService"/>/
/// <see cref="IGuiReportViewerService"/> boundary GUI-5's Results Dashboard already established
/// (see <see cref="IGuiReportExportService"/>'s own doc comment for why that is the ONLY way
/// <c>ServerSleuth.Gui</c> ever reaches <c>ReportArtifactFactory</c>/<c>IReportExporter</c>) — no
/// second export implementation, no <c>File.WriteAllText</c>/<c>File.WriteAllBytes</c> anywhere
/// here. Built fresh on every visit to <see cref="NavigationPage.Reports"/>, directly from
/// <c>ScanExecutionViewModel.State</c> (mirrors <see cref="DashboardOverviewViewModel"/>'s own
/// "rebuild on every visit" contract) — a stale "Exported: …"/report-viewer result from a
/// previous visit is never shown again on return.
/// </summary>
public sealed class ReportsOverviewViewModel : ObservableObject, IPageViewModel
{
    private readonly IGuiReportExportService? _exportService;
    private readonly IGuiReportViewerService? _viewerService;

    public ReportsOverviewViewModel(
        ScanExecutionState state, IGuiReportExportService? exportService = null, IGuiReportViewerService? viewerService = null)
    {
        State = state;
        _exportService = exportService;
        _viewerService = viewerService;
        Report = state.PipelineResult?.Report;

        _exportDirectory = state.OutputDirectory;
        _selectedReportFileName = ReportFileNames.FirstOrDefault() ?? string.Empty;

        StartScanCommand = new RelayCommand(_ => StartScanRequested?.Invoke(this, EventArgs.Empty));

        ExportReportCommand = new RelayCommand(
            _ => ExecuteExport(),
            _ => _exportService is not null && Report is not null && !string.IsNullOrWhiteSpace(ExportDirectory));

        OpenReportCommand = new RelayCommand(
            _ => ExecuteOpenReport(),
            _ => _viewerService is not null && !string.IsNullOrWhiteSpace(SelectedReportFileName));
    }

    public NavigationPage Page => NavigationPage.Reports;

    public ScanExecutionState State { get; }

    public ServerMigrationAssessmentReport? Report { get; }

    public bool HasResults => Report is not null;

    public bool HasNoResults => !HasResults;

    public string TargetDisplayName => State.TargetDisplayName;

    public TargetPlatform TargetPlatform => State.TargetPlatform;

    public DateTimeOffset? FinishedAt => State.FinishedAt;

    public ScanExecutionStatus Status => State.Status;

    // ----- REPORT ARTIFACTS (skill.md GUI-5 §2) -----
    public IReadOnlyList<string> ReportFileNames => State.OutputPaths;

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

    // ----- REPORT EXPORT (skill.md GUI-5 §1) — invokes the existing ReportArtifactFactory/
    // IReportExporter through IGuiReportExportService only; never fabricates success. -----

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

    public string LastExportedFileNamesText => string.Join(", ", LastExportResult?.WrittenFileNames ?? []);

    public RelayCommand ExportReportCommand { get; }

    private void ExecuteExport()
    {
        if (_exportService is null || Report is null)
        {
            return;
        }

        LastExportResult = _exportService.Export(Report, ExportDirectory, ExportFormat, ExportOverwritePolicy);
    }

    /// <summary>Raised by the empty-state "Start Scan" button.</summary>
    public event EventHandler? StartScanRequested;

    public RelayCommand StartScanCommand { get; }
}
