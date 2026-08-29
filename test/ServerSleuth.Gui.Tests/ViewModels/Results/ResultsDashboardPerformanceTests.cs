using System.Diagnostics;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.ViewModels.Results;

/// <summary>GUI-4 §Step26: an in-memory performance test with approximately 1,000 applications,
/// 10,000 findings, 5,000 dependencies, 10,000 actions, and 20,000 verification checks. The
/// fixture build itself (hand-authored data, not a pipeline run) is excluded from the measured
/// window — only <see cref="ResultsDashboardViewModel"/> CONSTRUCTION is timed, per the phase's
/// own "do not re-run the pipeline" instruction; this proves the DASHBOARD's own transformation
/// cost, not fixture-authoring cost.</summary>
public class ResultsDashboardPerformanceTests
{
    [Fact]
    public void Construction_OverALargeResult_CompletesWellUnderTenSeconds()
    {
        // 1,000 apps x (10 findings, 5 deps, 10 actions, 20 checks) = 10,000/5,000/10,000/20,000.
        var options = new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 1000,
            FindingsPerApplication = 10,
            DependenciesPerApplication = 5,
            ActionsPerApplication = 10,
            ChecksPerApplication = 20
        };
        var state = ScanResultFixtureFactory.BuildCompletedState(options);
        var pipeline = state.PipelineResult!;

        Assert.Equal(1000, pipeline.Report.ApplicationAssessments.Count);
        Assert.Equal(10_000, pipeline.Aggregation.Server.TotalFindingCount);
        Assert.Equal(5_000, pipeline.Report.ServerSummary.DependencyCount);
        Assert.Equal(10_000, pipeline.Report.Actions.Count);
        Assert.Equal(20_000, pipeline.Report.PreMigrationChecks.Count + pipeline.Report.PostMigrationChecks.Count);

        var stopwatch = Stopwatch.StartNew();
        var vm = new ResultsDashboardViewModel(state);
        stopwatch.Stop();

        Assert.Equal(1000, vm.Applications.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"ResultsDashboardViewModel construction took {stopwatch.Elapsed} for a 1,000-application result — expected well under 10s.");
    }

    [Fact]
    public void Filtering_OverALargeApplicationList_CompletesWellUnderOneSecond()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 1000, FindingsPerApplication = 3, DependenciesPerApplication = 1, ActionsPerApplication = 1, ChecksPerApplication = 1
        });
        var vm = new ResultsDashboardViewModel(state);

        var stopwatch = Stopwatch.StartNew();
        vm.SearchText = "Application 0";
        vm.SeverityFilter = vm.Applications[0].RiskSeverity;
        vm.OnlyWithIssues = true;
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"Filtering 1,000 applications took {stopwatch.Elapsed} — expected well under 1s.");
    }

    /// <summary>GUI-5 §13: the Export/Open orchestration itself must not introduce an accidental
    /// O(N²) traversal over the large result — it hands the whole already-computed
    /// <c>ServerMigrationAssessmentReport</c> reference straight to the export service (no
    /// per-application/per-finding loop of its own), so construction PLUS one export PLUS one
    /// open, together, should stay just as far under budget as construction alone did above.</summary>
    [Fact]
    public void ExportAndOpenReport_OverALargeResult_CompleteWellUnderOneSecond()
    {
        var options = new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 1000,
            FindingsPerApplication = 10,
            DependenciesPerApplication = 5,
            ActionsPerApplication = 10,
            ChecksPerApplication = 20
        };
        var state = ScanResultFixtureFactory.BuildCompletedState(options);
        var exportService = new ServerSleuth.Gui.Tests.Fakes.FakeGuiReportExportService();
        var viewerService = new ServerSleuth.Gui.Tests.Fakes.FakeGuiReportViewerService();
        var vm = new ResultsDashboardViewModel(state, exportService, viewerService);

        var stopwatch = Stopwatch.StartNew();
        vm.ExportReportCommand.Execute(null);
        vm.OpenReportCommand.Execute(null);
        stopwatch.Stop();

        Assert.Single(exportService.Calls);
        Assert.Single(viewerService.Calls);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"Export+Open orchestration over a 1,000-application result took {stopwatch.Elapsed} — expected well under 1s.");
    }
}
