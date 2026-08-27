using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Tests.Fixtures;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.ViewModels.Results;

/// <summary>GUI-4 §Step5-19: the Results Dashboard ViewModel reads real, already-computed
/// Risk/Migration data and never recomputes it. Every test here uses
/// <see cref="ScanResultFixtureFactory"/> — a hand-built fixture, never a real pipeline run
/// (Gui.Tests cannot reference Windows/Linux/Infrastructure).</summary>
public class ResultsDashboardViewModelTests
{
    [Fact]
    public void Page_IsResults()
    {
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState());
        Assert.Equal(NavigationPage.Results, vm.Page);
    }

    [Fact]
    public void HasResults_IsTrue_WhenPipelineResultIsPresent()
    {
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState());
        Assert.True(vm.HasResults);
        Assert.False(vm.HasNoResults);
    }

    [Fact]
    public void HasResults_IsFalse_ForACancelledScan_WithNoPipelineResult()
    {
        var state = ScanExecutionState.StartingFor(ServerSleuth.Core.Targets.ScanTarget.Local())
            .WithCompletion(ScanCompletionState.Cancelled());

        var vm = new ResultsDashboardViewModel(state);

        Assert.False(vm.HasResults);
        Assert.True(vm.HasNoResults);
        Assert.Empty(vm.Applications);
        Assert.Empty(vm.TopRisks);
        Assert.Empty(vm.Actions);
        Assert.Equal(AggregateSeverity.None, vm.OverallRiskSeverity);
    }

    [Fact]
    public void HasResults_IsFalse_ForAFailedScan_WithNoPipelineResult()
    {
        var state = ScanExecutionState.StartingFor(ServerSleuth.Core.Targets.ScanTarget.Local())
            .WithCompletion(ScanCompletionState.Failed("An unexpected error occurred during the scan. See application logs for details."));

        var vm = new ResultsDashboardViewModel(state);

        Assert.False(vm.HasResults);
        Assert.Empty(vm.Applications);
    }

    [Fact]
    public void RiskSummary_ReflectsTheActualServerRiskSummary_NeverARecomputedScore()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 4, FindingsPerApplication = 3 });
        var state = ScanResultFixtureFactory.BuildCompletedState();
        // Rebuild the state around this exact pipeline so counts are known to the assertion below.
        state = state with { PipelineResult = pipeline };

        var vm = new ResultsDashboardViewModel(state);

        var expected = pipeline.Aggregation.Server;
        Assert.Equal(expected.CriticalCount, vm.CriticalCount);
        Assert.Equal(expected.HighCount, vm.HighCount);
        Assert.Equal(expected.MediumCount, vm.MediumCount);
        Assert.Equal(expected.LowCount, vm.LowCount);
        Assert.Equal(expected.InfoCount, vm.InfoCount);
        Assert.Equal(expected.TotalFindingCount, vm.TotalFindingCount);
        Assert.Equal(expected.OverallSeverity, vm.OverallRiskSeverity);
        Assert.Same(expected.TopRisks, vm.TopRisks);
        Assert.Same(expected.Findings, vm.AllFindings);
    }

    [Fact]
    public void MigrationSummary_ReflectsTheActualServerMigrationSummary_NeverARecomputedStatus()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 6, FindingsPerApplication = 2 });
        var state = ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline };

        var vm = new ResultsDashboardViewModel(state);
        var expected = pipeline.Report.ServerSummary;

        Assert.Equal(expected.OverallMigrationStatus, vm.OverallMigrationStatus);
        Assert.Equal(expected.BlockedApplicationCount, vm.BlockedApplicationCount);
        Assert.Equal(expected.NeedsRemediationApplicationCount, vm.NeedsRemediationApplicationCount);
        Assert.Equal(expected.ReadyWithConditionsApplicationCount, vm.ReadyWithConditionsApplicationCount);
        Assert.Equal(expected.ReadyApplicationCount, vm.ReadyApplicationCount);
        Assert.Equal(expected.ApplicationCount, vm.ApplicationCount);
        // The four buckets must always sum back to the total application count — no application
        // silently dropped or double-counted by this ViewModel.
        Assert.Equal(expected.ApplicationCount,
            vm.BlockedApplicationCount + vm.NeedsRemediationApplicationCount + vm.ReadyWithConditionsApplicationCount + vm.ReadyApplicationCount);
    }

    [Fact]
    public void Applications_MatchesReportApplicationCount_InTheSameOrder()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 5, FindingsPerApplication = 1 });
        var state = ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline };

        var vm = new ResultsDashboardViewModel(state);

        Assert.Equal(pipeline.Report.ApplicationAssessments.Count, vm.Applications.Count);
        Assert.Equal(
            pipeline.Report.ApplicationAssessments.Select(a => a.Assessment.ApplicationBoundaryId),
            vm.Applications.Select(a => a.ApplicationBoundaryId));
    }

    [Fact]
    public void FilteredApplications_BySeverity_OnlyReturnsMatchingRows()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 5, FindingsPerApplication = 3 });
        var state = ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline };
        var vm = new ResultsDashboardViewModel(state);

        var target = vm.Applications.First().RiskSeverity;
        vm.SeverityFilter = target;

        Assert.NotEmpty(vm.FilteredApplications);
        Assert.All(vm.FilteredApplications, a => Assert.Equal(target, a.RiskSeverity));
        // The master list is never touched by filtering.
        Assert.Equal(5, vm.Applications.Count);
    }

    [Fact]
    public void FilteredApplications_BySearchText_IsCaseInsensitive()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 3, FindingsPerApplication = 1 });
        var state = ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline };
        var vm = new ResultsDashboardViewModel(state);

        vm.SearchText = "APPLICATION 00000";

        Assert.Single(vm.FilteredApplications);
        Assert.Equal("Application 00000", vm.FilteredApplications[0].ApplicationName);
    }

    [Fact]
    public void FilteredApplications_ClearingFilters_RestoresTheFullList()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 4, FindingsPerApplication = 1 });
        var state = ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline };
        var vm = new ResultsDashboardViewModel(state);

        vm.SearchText = "Application 00000";
        vm.SearchText = string.Empty;

        Assert.Equal(4, vm.FilteredApplications.Count);
    }

    [Fact]
    public void OnlyWithIssues_ExcludesApplicationsWithZeroIssues()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 3, FindingsPerApplication = 0 });
        var state = ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline };
        var vm = new ResultsDashboardViewModel(state);

        vm.OnlyWithIssues = true;

        Assert.Empty(vm.FilteredApplications);
        Assert.Equal(3, vm.Applications.Count);
    }

    [Fact]
    public void SelectApplicationCommand_SetsSelectedApplication_AndDetail()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 2, FindingsPerApplication = 1 });
        var state = ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline };
        var vm = new ResultsDashboardViewModel(state);
        var row = vm.Applications[0];

        Assert.Null(vm.SelectedApplication);
        vm.SelectApplicationCommand.Execute(row);

        Assert.Same(row, vm.SelectedApplication);
        Assert.Same(row.Detail, vm.SelectedApplicationDetail);
    }

    [Fact]
    public void SelectApplicationCommand_WithWrongParameterType_DoesNothing()
    {
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState());
        vm.SelectApplicationCommand.Execute("not an application row");
        Assert.Null(vm.SelectedApplication);
    }

    [Fact]
    public void DependencyGroups_ReflectsActualReportGrouping_NeverInventedFromStrings()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 5, DependenciesPerApplication = 2 });
        var state = ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline };
        var vm = new ResultsDashboardViewModel(state);

        Assert.Same(pipeline.Report.Dependencies, vm.DependencyGroups);
        Assert.Equal(pipeline.Report.Dependencies.Sum(g => g.Dependencies.Count), vm.DependencyGroups.Sum(g => g.Dependencies.Count));
    }

    [Fact]
    public void Coverage_IsCopiedVerbatim_AndNeverAltersMigrationStatus()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 2, FindingsPerApplication = 0 });
        var state = ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline };
        var vm = new ResultsDashboardViewModel(state);

        Assert.Equal(pipeline.Report.Coverage, vm.Coverage);
        // Zero findings anywhere still yields Ready — Coverage never independently downgrades it
        // (skill.md GUI-4 §14, preserving Phase 8C's own invariant).
        Assert.Equal(MigrationStatus.Ready, vm.OverallMigrationStatus);
    }

    [Fact]
    public void ScannerStatuses_And_ReportFileNames_AreReusedVerbatimFromScanExecutionState()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState();
        var vm = new ResultsDashboardViewModel(state);

        Assert.Same(state.ScannerStatuses, vm.ScannerStatuses);
        Assert.Same(state.OutputPaths, vm.ReportFileNames);
    }

    /// <summary>GUI-6 §3: "server-only findings" — issues scoped to the server itself (no
    /// application boundary attribution) must surface via <see cref="ResultsDashboardViewModel.ServerLevelIssues"/>
    /// and must NOT be attributed to any single application's own issue list.</summary>
    [Fact]
    public void ServerLevelIssues_AreExposedSeparately_AndNeverAttributedToAnApplication()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 2, FindingsPerApplication = 1, ServerLevelIssueCount = 3
        });
        var state = ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline };
        var vm = new ResultsDashboardViewModel(state);

        Assert.Equal(3, vm.ServerLevelIssues.Count);
        Assert.All(vm.ServerLevelIssues, issue => Assert.Empty(issue.AffectedBoundaryIds));
        Assert.All(vm.Applications, app => Assert.DoesNotContain(
            app.Detail.Issues, issue => vm.ServerLevelIssues.Select(i => i.IssueId).Contains(issue.IssueId)));
        // Server-level issues are a distinct list from the per-application issue set
        // (ServerMigrationAssessmentReport.ServerLevelIssues, Phase 8C) — never merged into it.
        Assert.DoesNotContain(vm.ServerLevelIssues, i1 => vm.AllMigrationIssues.Any(i2 => i2.IssueId == i1.IssueId));
    }

    /// <summary>GUI-6 §3: "shared infrastructure findings" — a dependency spanning more than one
    /// application boundary must surface via <see cref="ResultsDashboardViewModel.SharedInfrastructure"/>,
    /// never regrouped/reclassified from a string, and never duplicated per-application.</summary>
    [Fact]
    public void SharedInfrastructure_AttributedAcrossApplications_IsExposedAsOneEntry_NeverDuplicated()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 4, FindingsPerApplication = 1, SharedInfrastructureCount = 1
        });
        var state = ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline };
        var vm = new ResultsDashboardViewModel(state);

        var shared = Assert.Single(vm.SharedInfrastructure);
        Assert.Equal(4, shared.AffectedBoundaryIds.Count);
        Assert.Equal(vm.Applications.Select(a => a.ApplicationBoundaryId).OrderBy(id => id, StringComparer.Ordinal),
            shared.AffectedBoundaryIds.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void EmptyScan_ZeroApplications_ProducesEmptyCollections_NeverAnException()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 0, FindingsPerApplication = 0, DependenciesPerApplication = 0, ActionsPerApplication = 0, ChecksPerApplication = 0
        });
        var state = ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline };

        var vm = new ResultsDashboardViewModel(state);

        Assert.True(vm.HasResults);
        Assert.Empty(vm.Applications);
        Assert.Empty(vm.FilteredApplications);
        Assert.Empty(vm.TopRisks);
        Assert.Empty(vm.Actions);
        Assert.Empty(vm.DependencyGroups);
    }
}
