using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;

namespace ServerSleuth.Gui.ViewModels;

/// <summary>
/// GUI-7A's Dashboard — a lightweight, read-only summary of the LATEST already-completed scan.
/// Built fresh every time <see cref="NavigationPage.Dashboard"/> is navigated to (by
/// <see cref="MainViewModel"/>, from <c>ScanExecutionViewModel.State</c> — the exact same source
/// <see cref="Results.ResultsDashboardViewModel"/> already reads), never cached: unlike the
/// Results dashboard (built once per "View Results" click and held for that result's lifetime),
/// this page has no filter/selection state of its own, so re-reading the latest state on every
/// visit is both simpler and always current — including the moment right after a new scan
/// finishes, without requiring the user to click "View Results" first.
///
/// Every number here is copied verbatim from an already-computed summary
/// (<see cref="ServerMigrationSummary"/>/<see cref="Risk.Models.ServerRiskSummary"/>) or from
/// <see cref="ScanExecutionState"/> itself — never a new score, percentage, or recomputation
/// (skill.md's own "do not invent a 0-100 score" rule, carried over from GUI-4/GUI-6A).
/// </summary>
public sealed class DashboardOverviewViewModel : ObservableObject, IPageViewModel
{
    public DashboardOverviewViewModel(ScanExecutionState state)
    {
        State = state;

        var pipeline = state.PipelineResult;
        Report = pipeline?.Report;
        var serverRisk = pipeline?.Aggregation.Server;

        CriticalCount = serverRisk?.CriticalCount ?? 0;
        HighCount = serverRisk?.HighCount ?? 0;
        MediumCount = serverRisk?.MediumCount ?? 0;

        ApplicationCount = Report?.ServerSummary.ApplicationCount ?? 0;
        DependencyCount = Report?.ServerSummary.DependencyCount ?? 0;
        BlockedApplicationCount = Report?.ServerSummary.BlockedApplicationCount ?? 0;
        NeedsRemediationApplicationCount = Report?.ServerSummary.NeedsRemediationApplicationCount ?? 0;
        ReadyWithConditionsApplicationCount = Report?.ServerSummary.ReadyWithConditionsApplicationCount ?? 0;
        ReadyApplicationCount = Report?.ServerSummary.ReadyApplicationCount ?? 0;

        // GUI-8A: per-type inventory counts from the same Discovery.Entities the Inventory
        // Explorer already reads — counted by C# class so they are immune to Type-string
        // conventions varying across scanners (e.g. Runtime family names, NativeBinary vs Dll).
        var entities = pipeline?.Discovery.Entities ?? [];
        ApplicationEntityCount = entities.OfType<Application>().Count();
        DllEntityCount          = entities.OfType<Dll>().Count();
        ServiceEntityCount      = entities.OfType<Service>().Count();
        ComComponentEntityCount = entities.OfType<ComComponent>().Count();
        SoftwareEntityCount     = entities.OfType<Software>().Count();
        RuntimeEntityCount      = entities.OfType<Runtime>().Count();
        ScheduledTaskEntityCount = entities.OfType<ScheduledTask>().Count();
        CertificateEntityCount  = entities.OfType<Certificate>().Count();
        ConfigurationEntityCount = entities.OfType<Configuration>().Count();
        // ExternalDependencies come from DependencyExpansionEngine (Analysis layer), not raw
        // scanners — carried separately on ScanPipelineResult alongside Discovery.Entities, and
        // combined with them in InventoryExplorerViewModel the same way.
        ExternalConnectionCount = pipeline?.ExternalDependencies.Count ?? 0;

        // "Start Scan" (no scan yet) and "New Scan" (a completed one already exists) are the
        // SAME action — both route MainViewModel back to Scan Configuration, exactly like
        // ResultsDashboardViewModel.NewScanCommand/ScanExecutionViewModel.NewScanCommand already
        // do. One event, two buttons (XAML picks the label via HasResults).
        StartScanCommand = new RelayCommand(_ => StartScanRequested?.Invoke(this, EventArgs.Empty));

        // Only meaningful once a scan has actually produced a report — MainViewModel builds/
        // caches the real ResultsDashboardViewModel exactly as ScanExecutionViewModel.
        // ViewResultsCommand already does (no second construction path).
        ViewResultsCommand = new RelayCommand(_ => ViewResultsRequested?.Invoke(this, EventArgs.Empty), _ => HasResults);

        // Always reachable — InventoryExplorerViewModel already degrades to an explicit empty
        // state (skill.md GUI-6A §14) when there is nothing to show yet.
        ViewInventoryCommand = new RelayCommand(_ => ViewInventoryRequested?.Invoke(this, EventArgs.Empty));
    }

    public NavigationPage Page => NavigationPage.Dashboard;

    /// <summary>The latest scan's full non-sensitive state — the same object
    /// <see cref="Results.ResultsDashboardViewModel"/> and <see cref="Results.InventoryExplorerViewModel"/>
    /// are built from; never a second/independent copy.</summary>
    public ScanExecutionState State { get; }

    /// <summary>Null before any scan has completed, or when a scan never reached a consolidated
    /// report (Cancelled/Failed before analysis) — mirrors
    /// <see cref="Results.ResultsDashboardViewModel.Report"/> exactly.</summary>
    public ServerMigrationAssessmentReport? Report { get; }

    public bool HasResults => Report is not null;

    public bool HasNoResults => !HasResults;

    public bool IsPartial => State.Status == ScanExecutionStatus.Partial;

    // ----- LAST SCAN -----
    public ScanExecutionStatus Status => State.Status;
    public string TargetDisplayName => State.TargetDisplayName;
    public TargetPlatform TargetPlatform => State.TargetPlatform;

    // ----- DISCOVERY TOTALS ----- (EntityCount is the SAME field ScanSummary already shows —
    // never a second count recomputed from Discovery.Entities here.)
    public int EntityCount => State.EntityCount;
    public int ApplicationCount { get; }
    public int DependencyCount { get; }

    // ----- INVENTORY (GUI-8A) — per entity class from Discovery.Entities + ExternalDependencies;
    // counted by C# class (not by Type string) so runtime-family variance and Linux "NativeBinary"
    // type both land in the right bucket automatically. Zero when no scan has completed. -----
    public int ApplicationEntityCount { get; }
    public int DllEntityCount { get; }
    public int ServiceEntityCount { get; }
    public int ComComponentEntityCount { get; }
    public int SoftwareEntityCount { get; }
    public int RuntimeEntityCount { get; }
    public int ScheduledTaskEntityCount { get; }
    public int CertificateEntityCount { get; }
    public int ConfigurationEntityCount { get; }
    public int ExternalConnectionCount { get; }

    // ----- RISK ----- (only Critical/High/Medium — skill.md's own "concise summary" request;
    // the full Critical/High/Medium/Low/Info/Informational breakdown already exists on Results.)
    public int CriticalCount { get; }
    public int HighCount { get; }
    public int MediumCount { get; }

    // ----- MIGRATION -----
    public int BlockedApplicationCount { get; }
    public int NeedsRemediationApplicationCount { get; }
    public int ReadyWithConditionsApplicationCount { get; }
    public int ReadyApplicationCount { get; }

    /// <summary>Raised by the empty-state "Start Scan" button AND the populated-state "New Scan"
    /// button — <see cref="MainViewModel"/> handles both exactly like every other "go back to
    /// Scan Configuration" trigger (GUI-3's existing navigation flow, never a second one).</summary>
    public event EventHandler? StartScanRequested;

    /// <summary>Raised by "View Results" — <see cref="MainViewModel"/> builds/caches the real
    /// <see cref="Results.ResultsDashboardViewModel"/> exactly as it already does for
    /// <c>ScanExecutionViewModel.ViewResultsRequested</c>; no second construction path.</summary>
    public event EventHandler? ViewResultsRequested;

    /// <summary>Raised by "Inventory" — <see cref="MainViewModel"/> simply navigates to
    /// <see cref="NavigationPage.Inventory"/>; no data is built here.</summary>
    public event EventHandler? ViewInventoryRequested;

    public RelayCommand StartScanCommand { get; }
    public RelayCommand ViewResultsCommand { get; }
    public RelayCommand ViewInventoryCommand { get; }
}
