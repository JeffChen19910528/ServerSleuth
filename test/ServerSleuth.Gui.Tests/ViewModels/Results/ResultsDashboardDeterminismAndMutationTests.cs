using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Tests.Fixtures;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.ViewModels.Results;

/// <summary>GUI-4 §Step22-23: determinism and no-mutation guarantees. Repeated ViewModel
/// construction over the SAME already-completed <c>ScanExecutionState</c> must produce identical
/// collections/ordering every time, and building/filtering/selecting must never mutate the
/// underlying report/risk/migration/dependency/evidence objects.</summary>
public class ResultsDashboardDeterminismAndMutationTests
{
    private static ScanExecutionState BuildState() =>
        ScanResultFixtureFactory.BuildCompletedState(new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 8, FindingsPerApplication = 3, DependenciesPerApplication = 2, ActionsPerApplication = 2, ChecksPerApplication = 3
        });

    [Fact]
    public void RepeatedConstruction_OverTheSameState_ProducesIdenticalApplicationOrdering()
    {
        var state = BuildState();

        var first = new ResultsDashboardViewModel(state);
        var second = new ResultsDashboardViewModel(state);

        Assert.Equal(
            first.Applications.Select(a => a.ApplicationBoundaryId),
            second.Applications.Select(a => a.ApplicationBoundaryId));
    }

    [Fact]
    public void RepeatedConstruction_OverTheSameState_ProducesIdenticalRiskOrdering()
    {
        var state = BuildState();

        var first = new ResultsDashboardViewModel(state);
        var second = new ResultsDashboardViewModel(state);

        Assert.Equal(first.TopRisks.Select(f => f.Id), second.TopRisks.Select(f => f.Id));
        Assert.Equal(first.AllFindings.Select(f => f.Id), second.AllFindings.Select(f => f.Id));
    }

    [Fact]
    public void RepeatedConstruction_OverTheSameState_ProducesIdenticalDependencyOrdering()
    {
        var state = BuildState();

        var first = new ResultsDashboardViewModel(state);
        var second = new ResultsDashboardViewModel(state);

        Assert.Equal(
            first.DependencyGroups.SelectMany(g => g.Dependencies.Select(d => d.DependencyId)),
            second.DependencyGroups.SelectMany(g => g.Dependencies.Select(d => d.DependencyId)));
    }

    [Fact]
    public void RepeatedConstruction_IsNotDictionaryOrHashSetOrdered_SameOrderAcrossManyRuns()
    {
        var state = BuildState();

        var orderings = Enumerable.Range(0, 5)
            .Select(_ => new ResultsDashboardViewModel(state).Applications.Select(a => a.ApplicationBoundaryId).ToList())
            .ToList();

        Assert.All(orderings, o => Assert.Equal(orderings[0], o));
    }

    [Fact]
    public void Construction_DoesNotMutate_TheReportOrAggregation()
    {
        var state = BuildState();
        var pipeline = state.PipelineResult!;

        var reportBefore = System.Text.Json.JsonSerializer.Serialize(new
        {
            pipeline.Report.ServerSummary,
            AppCount = pipeline.Report.ApplicationAssessments.Count,
            IssueIds = pipeline.Report.Assessment.Server.Issues.Select(i => i.IssueId).ToList()
        });

        _ = new ResultsDashboardViewModel(state);

        var reportAfter = System.Text.Json.JsonSerializer.Serialize(new
        {
            pipeline.Report.ServerSummary,
            AppCount = pipeline.Report.ApplicationAssessments.Count,
            IssueIds = pipeline.Report.Assessment.Server.Issues.Select(i => i.IssueId).ToList()
        });

        Assert.Equal(reportBefore, reportAfter);
    }

    [Fact]
    public void Filtering_And_Selecting_NeverReordersOrShrinksTheMasterApplicationsList()
    {
        var state = BuildState();
        var vm = new ResultsDashboardViewModel(state);
        var originalOrder = vm.Applications.Select(a => a.ApplicationBoundaryId).ToList();

        vm.SearchText = "Application";
        vm.SeverityFilter = vm.Applications[0].RiskSeverity;
        vm.OnlyWithIssues = true;
        vm.SelectApplicationCommand.Execute(vm.Applications[0]);
        vm.SearchText = string.Empty;
        vm.SeverityFilter = null;
        vm.OnlyWithIssues = false;

        Assert.Equal(originalOrder, vm.Applications.Select(a => a.ApplicationBoundaryId));
    }

    [Fact]
    public void Filtering_ProducesANewListInstance_NeverTheSameReferenceAsApplications()
    {
        var vm = new ResultsDashboardViewModel(BuildState());
        vm.SearchText = "Application";

        Assert.NotSame(vm.Applications, vm.FilteredApplications);
    }

    [Fact]
    public void TwoDashboardsOverTheSameState_SelectingOnOneNeverAffectsTheOther()
    {
        var state = BuildState();
        var first = new ResultsDashboardViewModel(state);
        var second = new ResultsDashboardViewModel(state);

        first.SelectApplicationCommand.Execute(first.Applications[0]);

        Assert.NotNull(first.SelectedApplication);
        Assert.Null(second.SelectedApplication);
    }
}
