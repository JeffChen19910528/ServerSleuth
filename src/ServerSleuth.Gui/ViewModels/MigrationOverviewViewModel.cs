using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.ViewModels;

/// <summary>
/// GUI-7B's Migration page — a lightweight, read-only view of the latest completed scan's
/// migration assessment. Built fresh every time <see cref="NavigationPage.Migration"/> is
/// navigated to (mirrors <see cref="DashboardOverviewViewModel"/>/<see cref="Results.InventoryExplorerViewModel"/>'s
/// own "rebuild on every visit, own no state worth preserving across navigation" contract),
/// directly from <c>ScanExecutionViewModel.State</c> — the exact same source
/// <see cref="Results.ResultsDashboardViewModel"/> already reads. <see cref="Applications"/> is
/// built via <see cref="ApplicationRowViewModel.BuildFrom"/> — the IDENTICAL join
/// <see cref="Results.ResultsDashboardViewModel"/>'s own Applications list uses, never a second
/// migration/risk recomputation. Never touches <c>MigrationAssessmentEngine</c>/
/// <c>RiskRuleEngine</c>/any engine, and never calculates a new migration score (skill.md's own
/// "do not invent a 0-100 score" rule, carried over from GUI-4/GUI-6A/GUI-7A).
/// </summary>
public sealed class MigrationOverviewViewModel : ObservableObject, IPageViewModel
{
    public MigrationOverviewViewModel(ScanExecutionState state)
    {
        State = state;
        Report = state.PipelineResult?.Report;
        Applications = ApplicationRowViewModel.BuildFrom(state.PipelineResult);

        StartScanCommand = new RelayCommand(_ => StartScanRequested?.Invoke(this, EventArgs.Empty));
        SelectApplicationCommand = new RelayCommand(parameter =>
        {
            if (parameter is ApplicationRowViewModel row)
            {
                SelectedApplication = row;
            }
        });
    }

    public NavigationPage Page => NavigationPage.Migration;

    public ScanExecutionState State { get; }

    /// <summary>Null before any scan has completed, or when a scan never reached a consolidated
    /// report (Cancelled/Failed before analysis) — mirrors
    /// <see cref="Results.ResultsDashboardViewModel.Report"/> exactly.</summary>
    public ServerMigrationAssessmentReport? Report { get; }

    public bool HasResults => Report is not null;

    public bool HasNoResults => !HasResults;

    // ----- MIGRATION SUMMARY — copied verbatim from ServerMigrationSummary, never recomputed. -----
    public int BlockedApplicationCount => Report?.ServerSummary.BlockedApplicationCount ?? 0;
    public int NeedsRemediationApplicationCount => Report?.ServerSummary.NeedsRemediationApplicationCount ?? 0;
    public int ReadyWithConditionsApplicationCount => Report?.ServerSummary.ReadyWithConditionsApplicationCount ?? 0;
    public int ReadyApplicationCount => Report?.ServerSummary.ReadyApplicationCount ?? 0;

    // ----- APPLICATION MIGRATION LIST -----
    public IReadOnlyList<ApplicationRowViewModel> Applications { get; }

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

    /// <summary>The SAME <see cref="ApplicationDetailViewModel"/>/<see cref="Views.ApplicationDetailView"/>
    /// GUI-4's Results Dashboard already uses for its own "select an application" panel — issues/
    /// actions/verification checks/dependencies, declarative only, no Execute/Apply/Install/
    /// Restart affordance anywhere in it.</summary>
    public ApplicationDetailViewModel? SelectedApplicationDetail => SelectedApplication?.Detail;

    public RelayCommand SelectApplicationCommand { get; }

    /// <summary>Raised by the empty-state "Start Scan" button — <see cref="MainViewModel"/>
    /// handles it exactly like every other "go back to Scan Configuration" trigger.</summary>
    public event EventHandler? StartScanRequested;

    public RelayCommand StartScanCommand { get; }
}
