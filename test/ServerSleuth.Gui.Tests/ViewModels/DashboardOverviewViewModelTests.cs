using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>GUI-7A: the lightweight Dashboard overview reads real, already-computed data off an
/// already-completed <see cref="ScanExecutionState"/> and never recomputes/fabricates anything.
/// Every test uses <see cref="ScanResultFixtureFactory"/> — a hand-built fixture, never a real
/// pipeline run (Gui.Tests cannot reference Windows/Linux/Infrastructure); constructing this
/// ViewModel touches no orchestration type at all (see <c>NoScanExecutionFromGuiTests</c>).</summary>
public class DashboardOverviewViewModelTests
{
    [Fact]
    public void Page_IsDashboard()
    {
        var vm = new DashboardOverviewViewModel(ScanExecutionState.Idle);
        Assert.Equal(NavigationPage.Dashboard, vm.Page);
    }

    // ----- 1. Empty state -----
    [Fact]
    public void NoScanYet_ShowsTheEmptyState_WithNoFabricatedStatistics()
    {
        var vm = new DashboardOverviewViewModel(ScanExecutionState.Idle);

        Assert.False(vm.HasResults);
        Assert.True(vm.HasNoResults);
        Assert.Null(vm.Report);
        Assert.Equal(0, vm.EntityCount);
        Assert.Equal(0, vm.ApplicationCount);
        Assert.Equal(0, vm.DependencyCount);
        Assert.Equal(0, vm.CriticalCount);
        Assert.Equal(0, vm.HighCount);
        Assert.Equal(0, vm.MediumCount);
        Assert.Equal(0, vm.BlockedApplicationCount);
        Assert.Equal(0, vm.NeedsRemediationApplicationCount);
        Assert.Equal(0, vm.ReadyWithConditionsApplicationCount);
        Assert.Equal(0, vm.ReadyApplicationCount);
        Assert.False(vm.ViewResultsCommand.CanExecute(null));
    }

    // ----- 2. Populated from a real ScanPipelineResult fixture -----
    [Fact]
    public void CompletedScan_ShowsTheRealSummary()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options { ApplicationCount = 3, FindingsPerApplication = 2, DependenciesPerApplication = 1 });
        var vm = new DashboardOverviewViewModel(state);

        Assert.True(vm.HasResults);
        Assert.False(vm.HasNoResults);
        Assert.NotNull(vm.Report);
        Assert.True(vm.ApplicationCount > 0);
        Assert.True(vm.ViewResultsCommand.CanExecute(null));
    }

    // ----- 3./4. Statistics match the source data exactly — never a recomputed/transformed
    // value (no new 0-100 score, no re-derived risk/migration semantics). -----
    [Fact]
    public void Statistics_AreCopiedVerbatim_FromTheSameSourceResultsDashboardViewModelReads()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options { ApplicationCount = 4, FindingsPerApplication = 3, DependenciesPerApplication = 2 });
        var pipeline = state.PipelineResult!;
        var serverRisk = pipeline.Aggregation.Server;
        var serverSummary = pipeline.Report.ServerSummary;

        var vm = new DashboardOverviewViewModel(state);

        Assert.Equal(serverRisk.CriticalCount, vm.CriticalCount);
        Assert.Equal(serverRisk.HighCount, vm.HighCount);
        Assert.Equal(serverRisk.MediumCount, vm.MediumCount);
        Assert.Equal(serverSummary.ApplicationCount, vm.ApplicationCount);
        Assert.Equal(serverSummary.DependencyCount, vm.DependencyCount);
        Assert.Equal(serverSummary.BlockedApplicationCount, vm.BlockedApplicationCount);
        Assert.Equal(serverSummary.NeedsRemediationApplicationCount, vm.NeedsRemediationApplicationCount);
        Assert.Equal(serverSummary.ReadyWithConditionsApplicationCount, vm.ReadyWithConditionsApplicationCount);
        Assert.Equal(serverSummary.ReadyApplicationCount, vm.ReadyApplicationCount);
        Assert.Equal(state.EntityCount, vm.EntityCount);
    }

    // ----- 6. Deterministic output -----
    [Fact]
    public void BuildingTwiceFromTheSameState_ProducesIdenticalStatistics()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options { ApplicationCount = 5, FindingsPerApplication = 2 });

        var first = new DashboardOverviewViewModel(state);
        var second = new DashboardOverviewViewModel(state);

        Assert.Equal(first.CriticalCount, second.CriticalCount);
        Assert.Equal(first.HighCount, second.HighCount);
        Assert.Equal(first.MediumCount, second.MediumCount);
        Assert.Equal(first.ApplicationCount, second.ApplicationCount);
        Assert.Equal(first.DependencyCount, second.DependencyCount);
        Assert.Equal(first.BlockedApplicationCount, second.BlockedApplicationCount);
        Assert.Equal(first.EntityCount, second.EntityCount);
    }

    // ----- 7. Partial scan remains visibly partial — never shown as a full success. -----
    [Fact]
    public void PartialScan_IsSurfacedAsPartial_WithItsRealNonZeroStatistics()
    {
        var options = new ScanResultFixtureFactory.Options { ApplicationCount = 2, FindingsPerApplication = 1 };
        var state = ScanResultFixtureFactory.BuildCompletedState(options, ScanExecutionStatus.Partial);

        var vm = new DashboardOverviewViewModel(state);

        Assert.True(vm.IsPartial);
        Assert.Equal(ScanExecutionStatus.Partial, vm.Status);
        Assert.True(vm.HasResults);
        Assert.True(vm.ApplicationCount > 0);
    }

    [Fact]
    public void CompletedScan_IsNeverShownAsPartial()
    {
        var vm = new DashboardOverviewViewModel(ScanResultFixtureFactory.BuildCompletedState());
        Assert.False(vm.IsPartial);
    }

    // ----- 8. Zero-entity/zero-application scan — real zeros, never fabricated non-zero data. -----
    [Fact]
    public void ZeroApplicationScan_ShowsRealZeroes_NeverFabricatedNumbers()
    {
        var options = new ScanResultFixtureFactory.Options { ApplicationCount = 0 };
        var state = ScanResultFixtureFactory.BuildCompletedState(options);

        var vm = new DashboardOverviewViewModel(state);

        Assert.True(vm.HasResults);
        Assert.Equal(0, vm.ApplicationCount);
        Assert.Equal(0, vm.DependencyCount);
        Assert.Equal(0, vm.CriticalCount);
        Assert.Equal(0, vm.HighCount);
        Assert.Equal(0, vm.MediumCount);
        Assert.Equal(0, vm.BlockedApplicationCount);
    }

    [Fact]
    public void CancelledScan_HasNoResults_NeverAFabricatedSummary()
    {
        var state = ScanExecutionState.StartingFor(ServerSleuth.Core.Targets.ScanTarget.Local())
            .WithCompletion(ScanCompletionState.Cancelled());

        var vm = new DashboardOverviewViewModel(state);

        Assert.False(vm.HasResults);
        Assert.Null(vm.Report);
        Assert.Equal(0, vm.ApplicationCount);
    }

    [Fact]
    public void FailedScan_HasNoResults_NeverAFabricatedSummary()
    {
        var state = ScanExecutionState.StartingFor(ServerSleuth.Core.Targets.ScanTarget.Local())
            .WithCompletion(ScanCompletionState.Failed("An unexpected error occurred during the scan. See application logs for details."));

        var vm = new DashboardOverviewViewModel(state);

        Assert.False(vm.HasResults);
        Assert.Null(vm.Report);
    }

    [Fact]
    public void ViewInventoryCommand_IsAlwaysExecutable_EvenWithNoScanYet()
    {
        var vm = new DashboardOverviewViewModel(ScanExecutionState.Idle);
        Assert.True(vm.ViewInventoryCommand.CanExecute(null));
    }

    [Fact]
    public void StartScanCommand_RaisesStartScanRequested()
    {
        var vm = new DashboardOverviewViewModel(ScanExecutionState.Idle);
        var raised = false;
        vm.StartScanRequested += (_, _) => raised = true;

        vm.StartScanCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void ViewResultsCommand_RaisesViewResultsRequested()
    {
        var vm = new DashboardOverviewViewModel(ScanResultFixtureFactory.BuildCompletedState());
        var raised = false;
        vm.ViewResultsRequested += (_, _) => raised = true;

        vm.ViewResultsCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void ViewInventoryCommand_RaisesViewInventoryRequested()
    {
        var vm = new DashboardOverviewViewModel(ScanExecutionState.Idle);
        var raised = false;
        vm.ViewInventoryRequested += (_, _) => raised = true;

        vm.ViewInventoryCommand.Execute(null);

        Assert.True(raised);
    }
}
