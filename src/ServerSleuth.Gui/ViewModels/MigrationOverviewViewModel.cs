using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Preparation;
using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Core.Models;
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
        PreparationSummary = BuildPreparationSummary(state.PipelineResult, Report);

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

    // ----- GUI-8C: MIGRATION CHECKLIST — aggregate inventory counts across all applications -----

    public int TotalDllBinaryCount          => Applications.Sum(a => a.Detail?.Components?.DllBinaryCount          ?? 0);
    public int TotalRuntimeCount            => Applications.Sum(a => a.Detail?.Components?.RuntimeCount            ?? 0);
    public int TotalServiceCount            => Applications.Sum(a => a.Detail?.Components?.ServiceCount            ?? 0);
    public int TotalComComponentCount       => Applications.Sum(a => a.Detail?.Components?.ComComponentCount       ?? 0);
    public int TotalSoftwareCount           => Applications.Sum(a => a.Detail?.Components?.SoftwareCount           ?? 0);
    public int TotalScheduledTaskCount      => Applications.Sum(a => a.Detail?.Components?.ScheduledTaskCount      ?? 0);
    public int TotalCertificateCount        => Applications.Sum(a => a.Detail?.Components?.CertificateCount        ?? 0);
    public int TotalConfigurationCount      => Applications.Sum(a => a.Detail?.Components?.ConfigurationCount      ?? 0);
    public int TotalExternalConnectionCount => Applications.Sum(a => a.Detail?.Components?.ExternalConnectionCount ?? 0);

    public int TotalComponentCount =>
        TotalDllBinaryCount + TotalRuntimeCount + TotalServiceCount + TotalComComponentCount +
        TotalSoftwareCount + TotalScheduledTaskCount + TotalCertificateCount +
        TotalConfigurationCount + TotalExternalConnectionCount;

    public bool HasAnyComponents => TotalComponentCount > 0;

    // ----- GUI-10: MIGRATION PREPARATION — inventory-derived, built by the exact same
    // MigrationIntentCatalog/MigrationPreparationSummaryBuilder GUI-9B built for the JSON/HTML
    // reports (relocated to ServerSleuth.Analysis so both sides can share it without ServerSleuth.Gui
    // ever referencing ServerSleuth.Reporting — see MigrationIntent.cs's own doc comment). Never
    // recalculated here: this ViewModel only supplies already-computed, server-wide, unfiltered
    // per-category counts (the same counts DashboardOverviewViewModel's own *EntityCount
    // properties already compute the identical way) and lets the shared builder do the mapping.
    //
    // Deliberately NOT built from the per-application Total*Count sums above: those intentionally
    // double-count a shared entity once per application boundary it belongs to, which is correct
    // for "how many components does each application need" but wrong for "how many DISTINCT
    // server-wide items must be prepared" — the same reasoning ReportDtoMapper already applied
    // when it summed InventoryEntityDto list counts, not per-application sums (skill.md GUI-9B §8,
    // GUI-10 §5, §11).
    public MigrationPreparationSummary PreparationSummary { get; }

    public bool HasAnyPreparation => PreparationSummary.TotalInventoryCount > 0;

    public int DeployCount => PreparationCount(MigrationIntent.Deploy);
    public int InstallCount => PreparationCount(MigrationIntent.Install);
    public int CreateCount => PreparationCount(MigrationIntent.Create);
    public int RegisterCount => PreparationCount(MigrationIntent.Register);
    public int ConfigureCount => PreparationCount(MigrationIntent.Configure);
    public int VerifyCount => PreparationCount(MigrationIntent.Verify);
    public int ReviewCount => PreparationCount(MigrationIntent.Review);

    private int PreparationCount(MigrationIntent intent) =>
        PreparationSummary.IntentCounts.Single(i => i.Intent == intent).Count;

    private static MigrationPreparationSummary BuildPreparationSummary(
        ScanPipelineResult? pipeline, ServerMigrationAssessmentReport? report)
    {
        var entities = pipeline?.Discovery.Entities ?? [];

        var categoryCounts = new (string Category, int Count)[]
        {
            (MigrationIntentCatalog.ApplicationCategory, report?.ApplicationAssessments.Count ?? 0),
            ("Dll", entities.OfType<Dll>().Count()),
            ("Runtime", entities.OfType<Runtime>().Count()),
            ("Service", entities.OfType<Service>().Count()),
            ("ComComponent", entities.OfType<ComComponent>().Count()),
            ("Software", entities.OfType<Software>().Count()),
            ("ScheduledTask", entities.OfType<ScheduledTask>().Count()),
            ("Certificate", entities.OfType<Certificate>().Count()),
            ("Configuration", entities.OfType<Configuration>().Count()),
            ("ExternalDependency", pipeline?.ExternalDependencies.Count ?? 0)
        };

        return MigrationPreparationSummaryBuilder.Build(categoryCounts);
    }

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
