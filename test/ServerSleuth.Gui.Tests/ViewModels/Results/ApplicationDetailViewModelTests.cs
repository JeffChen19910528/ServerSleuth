using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Gui.Tests.Fixtures;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.ViewModels.Results;

/// <summary>GUI-4 §Step7, §Step25: application detail must show exactly the selected
/// application's own data — every list is a reference into the underlying already-consolidated
/// record, never recomputed, and a server-level finding is never misattributed to an
/// application.</summary>
public class ApplicationDetailViewModelTests
{
    [Fact]
    public void Detail_ForAnApplicationWithMultipleFindings_ExposesAllOfThem()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 3, FindingsPerApplication = 4 });
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline });

        var detail = vm.Applications[0].Detail;

        Assert.Equal(4, detail.FindingCount);
        Assert.Equal(4, detail.AllFindings.Count);
        Assert.All(detail.AllFindings, f => Assert.Equal(detail.ApplicationBoundaryId, f.ApplicationBoundaryId));
    }

    [Fact]
    public void Detail_NeverIncludesAnotherApplicationsFindings()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 4, FindingsPerApplication = 2 });
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline });

        foreach (var row in vm.Applications)
        {
            Assert.All(row.Detail.AllFindings, f => Assert.Equal(row.ApplicationBoundaryId, f.ApplicationBoundaryId));
            Assert.All(row.Detail.Issues, i => Assert.Contains(row.ApplicationBoundaryId, i.AffectedBoundaryIds));
        }
    }

    [Fact]
    public void Detail_ForAnApplicationWithNoFindings_HasEmptyRiskSection_NotNull()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 1, FindingsPerApplication = 0 });
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline });

        var detail = vm.Applications[0].Detail;

        Assert.Equal(0, detail.FindingCount);
        Assert.Empty(detail.TopRisks);
        Assert.Empty(detail.AllFindings);
        Assert.Equal(AggregateSeverity.None, detail.RiskSeverity);
        Assert.Null(vm.Applications[0].Detail.Risk);
    }

    [Fact]
    public void Detail_MigrationDependencies_AreScopedToThisApplication()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 3, DependenciesPerApplication = 2, FindingsPerApplication = 1 });
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline });

        foreach (var row in vm.Applications)
        {
            Assert.Equal(2, row.Detail.Dependencies.Count);
            Assert.All(row.Detail.Dependencies, d => Assert.Contains(row.ApplicationBoundaryId, d.AffectedBoundaryIds));
        }
    }

    [Fact]
    public void Detail_ActionsAndChecks_AreReferencesFromTheOriginatingApplicationMigrationSummary_NeverCopies()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options { ApplicationCount = 2, FindingsPerApplication = 1, ActionsPerApplication = 1, ChecksPerApplication = 2 });
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline });

        var appSummary = pipeline.Report.ApplicationAssessments[0];
        var detail = vm.Applications[0].Detail;

        Assert.Same(appSummary.Actions, detail.Actions);
        Assert.Same(appSummary.PreMigrationChecks, detail.PreMigrationChecks);
        Assert.Same(appSummary.PostMigrationChecks, detail.PostMigrationChecks);
    }
}
