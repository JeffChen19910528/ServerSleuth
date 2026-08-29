using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>GUI-7B: the lightweight Migration page reads real, already-computed data off an
/// already-completed <see cref="ScanExecutionState"/> and never recomputes/fabricates anything —
/// no <c>MigrationAssessmentEngine</c>/<c>RiskRuleEngine</c> dependency exists anywhere on this
/// type (see <c>NoScanExecutionFromGuiTests</c>). Every test uses
/// <see cref="ScanResultFixtureFactory"/> — a hand-built fixture, never a real pipeline run.</summary>
public class MigrationOverviewViewModelTests
{
    [Fact]
    public void Page_IsMigration()
    {
        var vm = new MigrationOverviewViewModel(ScanExecutionState.Idle);
        Assert.Equal(NavigationPage.Migration, vm.Page);
    }

    // ----- 1. Empty state -----
    [Fact]
    public void NoScanYet_ShowsTheEmptyState_WithNoFabricatedStatistics()
    {
        var vm = new MigrationOverviewViewModel(ScanExecutionState.Idle);

        Assert.False(vm.HasResults);
        Assert.True(vm.HasNoResults);
        Assert.Null(vm.Report);
        Assert.Empty(vm.Applications);
        Assert.Equal(0, vm.BlockedApplicationCount);
        Assert.Equal(0, vm.NeedsRemediationApplicationCount);
        Assert.Equal(0, vm.ReadyWithConditionsApplicationCount);
        Assert.Equal(0, vm.ReadyApplicationCount);
    }

    // ----- 2./3. Summary counts match the real MigrationAssessmentSummary — never recalculated. -----
    [Fact]
    public void SummaryCounts_AreCopiedVerbatim_FromTheSameServerSummary_ResultsDashboardViewModelReads()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options { ApplicationCount = 6, FindingsPerApplication = 2 });
        var serverSummary = state.PipelineResult!.Report.ServerSummary;

        var vm = new MigrationOverviewViewModel(state);

        Assert.True(vm.HasResults);
        Assert.Equal(serverSummary.BlockedApplicationCount, vm.BlockedApplicationCount);
        Assert.Equal(serverSummary.NeedsRemediationApplicationCount, vm.NeedsRemediationApplicationCount);
        Assert.Equal(serverSummary.ReadyWithConditionsApplicationCount, vm.ReadyWithConditionsApplicationCount);
        Assert.Equal(serverSummary.ReadyApplicationCount, vm.ReadyApplicationCount);
    }

    // ----- 3./4. Application list matches source, deterministic ordering -----
    [Fact]
    public void ApplicationList_MatchesTheSourceReport_InTheReportsOwnOrder()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options { ApplicationCount = 5, FindingsPerApplication = 1 });
        var expectedNames = state.PipelineResult!.Report.ApplicationAssessments
            .Select(a => a.Assessment.ApplicationBoundaryName)
            .ToList();

        var vm = new MigrationOverviewViewModel(state);

        Assert.Equal(expectedNames, vm.Applications.Select(a => a.ApplicationName));
    }

    [Fact]
    public void ApplicationList_Ordering_IsDeterministic_AcrossIndependentBuilds()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options { ApplicationCount = 7, FindingsPerApplication = 1 });

        var first = new MigrationOverviewViewModel(state);
        var second = new MigrationOverviewViewModel(state);

        Assert.Equal(first.Applications.Select(a => a.ApplicationBoundaryId), second.Applications.Select(a => a.ApplicationBoundaryId));
    }

    // ----- 5./6./7./8. Selecting an application exposes real issues/actions/verification/dependencies -----
    [Fact]
    public void SelectingAnApplication_ExposesItsRealIssuesActionsChecksAndDependencies()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options
            {
                ApplicationCount = 1, FindingsPerApplication = 3, DependenciesPerApplication = 2, ActionsPerApplication = 1, ChecksPerApplication = 2
            });
        var expectedDetail = state.PipelineResult!.Report.ApplicationAssessments[0];

        var vm = new MigrationOverviewViewModel(state);
        vm.SelectApplicationCommand.Execute(vm.Applications[0]);

        Assert.Same(vm.Applications[0], vm.SelectedApplication);
        Assert.NotNull(vm.SelectedApplicationDetail);
        Assert.Same(expectedDetail.Assessment.Issues, vm.SelectedApplicationDetail!.Issues);
        Assert.Same(expectedDetail.Actions, vm.SelectedApplicationDetail.Actions);
        Assert.Same(expectedDetail.PreMigrationChecks, vm.SelectedApplicationDetail.PreMigrationChecks);
        Assert.Same(expectedDetail.PostMigrationChecks, vm.SelectedApplicationDetail.PostMigrationChecks);
        Assert.Same(expectedDetail.Assessment.Dependencies, vm.SelectedApplicationDetail.Dependencies);
        Assert.NotEmpty(vm.SelectedApplicationDetail.Issues);
        Assert.NotEmpty(vm.SelectedApplicationDetail.Actions);
        Assert.NotEmpty(vm.SelectedApplicationDetail.Dependencies);
    }

    // ----- 9. No mutation -----
    [Fact]
    public void SelectingAnApplication_NeverMutatesTheMasterApplicationsList()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options { ApplicationCount = 4, FindingsPerApplication = 1 });
        var vm = new MigrationOverviewViewModel(state);
        var originalOrder = vm.Applications.Select(a => a.ApplicationBoundaryId).ToList();

        vm.SelectApplicationCommand.Execute(vm.Applications[2]);
        vm.SelectApplicationCommand.Execute(vm.Applications[0]);

        Assert.Equal(originalOrder, vm.Applications.Select(a => a.ApplicationBoundaryId));
    }

    // ----- 12. Partial/failed/empty scan handling -----
    [Fact]
    public void PartialScan_StillShowsItsRealNonZeroStatistics()
    {
        var options = new ScanResultFixtureFactory.Options { ApplicationCount = 3, FindingsPerApplication = 1 };
        var state = ScanResultFixtureFactory.BuildCompletedState(options, ScanExecutionStatus.Partial);

        var vm = new MigrationOverviewViewModel(state);

        Assert.True(vm.HasResults);
        Assert.NotEmpty(vm.Applications);
    }

    [Fact]
    public void FailedScan_HasNoResults_NeverAFabricatedSummary()
    {
        var state = ScanExecutionState.StartingFor(ServerSleuth.Core.Targets.ScanTarget.Local())
            .WithCompletion(ScanCompletionState.Failed("An unexpected error occurred during the scan. See application logs for details."));

        var vm = new MigrationOverviewViewModel(state);

        Assert.False(vm.HasResults);
        Assert.Empty(vm.Applications);
    }

    [Fact]
    public void CancelledScan_HasNoResults_NeverAFabricatedSummary()
    {
        var state = ScanExecutionState.StartingFor(ServerSleuth.Core.Targets.ScanTarget.Local())
            .WithCompletion(ScanCompletionState.Cancelled());

        var vm = new MigrationOverviewViewModel(state);

        Assert.False(vm.HasResults);
        Assert.Empty(vm.Applications);
    }

    [Fact]
    public void ZeroApplicationScan_ShowsRealZeroes_NeverFabricatedNumbers()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(new ScanResultFixtureFactory.Options { ApplicationCount = 0 });

        var vm = new MigrationOverviewViewModel(state);

        Assert.True(vm.HasResults);
        Assert.Empty(vm.Applications);
        Assert.Equal(0, vm.BlockedApplicationCount);
    }

    [Fact]
    public void StartScanCommand_RaisesStartScanRequested()
    {
        var vm = new MigrationOverviewViewModel(ScanExecutionState.Idle);
        var raised = false;
        vm.StartScanRequested += (_, _) => raised = true;

        vm.StartScanCommand.Execute(null);

        Assert.True(raised);
    }
}
