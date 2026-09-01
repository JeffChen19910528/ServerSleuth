using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.Tests.Fakes;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>GUI-7B: the lightweight Reports page reuses the EXACT SAME
/// <see cref="ServerSleuth.Gui.Services.IGuiReportExportService"/>/<see cref="ServerSleuth.Gui.Services.IGuiReportViewerService"/>
/// boundary GUI-5's Results Dashboard already established — every test here proves that (never a
/// second export/read implementation), using the same fakes
/// <c>ResultsDashboardExportAndViewerTests</c> already uses.</summary>
public class ReportsOverviewViewModelTests
{
    [Fact]
    public void Page_IsReports()
    {
        var vm = new ReportsOverviewViewModel(ScanExecutionState.Idle);
        Assert.Equal(NavigationPage.Reports, vm.Page);
    }

    // ----- 13. Empty report state -----
    [Fact]
    public void NoScanYet_ShowsTheEmptyState_WithNoFabricatedReportEntry()
    {
        var vm = new ReportsOverviewViewModel(ScanExecutionState.Idle);

        Assert.False(vm.HasResults);
        Assert.True(vm.HasNoResults);
        Assert.Empty(vm.ReportFileNames);
    }

    // ----- 14. Existing report displayed -----
    [Fact]
    public void CompletedScan_ShowsItsRealReportFileNames()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState();

        var vm = new ReportsOverviewViewModel(state);

        Assert.True(vm.HasResults);
        Assert.Equal(state.OutputPaths, vm.ReportFileNames);
    }

    // ----- 15. Open uses the existing viewer service -----
    [Fact]
    public void OpenReportCommand_CallsTheInjectedViewerService_WithTheSelectedFileName()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState();
        var viewer = new FakeGuiReportViewerService { ResultToReturn = GuiReportViewResult.Succeeded("{\"ok\":true}") };
        var vm = new ReportsOverviewViewModel(state, viewerService: viewer) { SelectedReportFileName = "report.json" };

        vm.OpenReportCommand.Execute(null);

        Assert.Single(viewer.Calls);
        Assert.Equal((state.OutputDirectory, "report.json"), viewer.Calls[0]);
        Assert.True(vm.ReportViewResult!.Success);
        Assert.Equal("{\"ok\":true}", vm.ReportViewResult.Content);
    }

    // ----- 16. Export uses the existing export service -----
    [Fact]
    public void ExportReportCommand_CallsTheInjectedExportService_WithTheChosenOptions()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState();
        var exporter = new FakeGuiReportExportService { ResultToReturn = GuiReportExportResult.Succeeded(["report.json"]) };
        var vm = new ReportsOverviewViewModel(state, exporter)
        {
            ExportDirectory = "./out2", ExportFormat = ScanOutputFormat.Json, ExportOverwritePolicy = ScanOverwritePolicy.Overwrite
        };

        vm.ExportReportCommand.Execute(null);

        Assert.Single(exporter.Calls);
        var call = exporter.Calls[0];
        Assert.Same(state.PipelineResult, call.Pipeline);
        Assert.Equal("./out2", call.OutputDirectory);
        Assert.Equal(ScanOutputFormat.Json, call.Format);
        Assert.Equal(ScanOverwritePolicy.Overwrite, call.OverwritePolicy);
        Assert.True(vm.LastExportResult!.Success);
    }

    // ----- 17. No second report rendering pipeline (structural — same fakes/interfaces as
    // Results, never a locally-constructed exporter/renderer). Reinforced by NoScanExecutionFromGuiTests. -----
    [Fact]
    public void ExportReportCommand_IsDisabled_WhenNoExportServiceWasSupplied()
    {
        var vm = new ReportsOverviewViewModel(ScanResultFixtureFactory.BuildCompletedState());
        Assert.False(vm.ExportReportCommand.CanExecute(null));
    }

    [Fact]
    public void OpenReportCommand_IsDisabled_WhenNoViewerServiceWasSupplied()
    {
        var vm = new ReportsOverviewViewModel(ScanResultFixtureFactory.BuildCompletedState()) { SelectedReportFileName = "report.json" };
        Assert.False(vm.OpenReportCommand.CanExecute(null));
    }

    // ----- 19. Export failure is represented correctly -----
    [Fact]
    public void ExportFailure_IsSurfacedVerbatim_NeverSilentlyTreatedAsSuccess()
    {
        var exporter = new FakeGuiReportExportService
        {
            ResultToReturn = GuiReportExportResult.Failed(GuiReportExportFailureReason.AlreadyExists, "report.json already exists.")
        };
        var vm = new ReportsOverviewViewModel(ScanResultFixtureFactory.BuildCompletedState(), exporter) { ExportDirectory = "./out" };

        vm.ExportReportCommand.Execute(null);

        Assert.False(vm.LastExportResult!.Success);
        Assert.Equal("report.json already exists.", vm.LastExportResult.ErrorMessage);
    }

    // ----- 20. Credentials are not exposed — structural, see NoCredentialShapedGuiStateTests/
    // ResultsDashboardSecurityBoundaryTests (both extended with this type). -----

    // ----- 21. HTML is displayed as plain text -----
    [Fact]
    public void OpenReportCommand_OnAnHtmlFile_ShowsItsRawTextContent_NeverRendersIt()
    {
        const string html = "<html><body><script>alert(1)</script></body></html>";
        var viewer = new FakeGuiReportViewerService { ResultToReturn = GuiReportViewResult.Succeeded(html) };
        var vm = new ReportsOverviewViewModel(ScanResultFixtureFactory.BuildCompletedState(), viewerService: viewer)
        {
            SelectedReportFileName = "report.html"
        };

        vm.OpenReportCommand.Execute(null);

        Assert.Equal(html, vm.ReportViewResult!.Content);
    }

    [Fact]
    public void StartScanCommand_RaisesStartScanRequested()
    {
        var vm = new ReportsOverviewViewModel(ScanExecutionState.Idle);
        var raised = false;
        vm.StartScanRequested += (_, _) => raised = true;

        vm.StartScanCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void CancelledScan_HasNoResults_NeverAFabricatedReportEntry()
    {
        var state = ScanExecutionState.StartingFor(ServerSleuth.Core.Targets.ScanTarget.Local())
            .WithCompletion(ScanCompletionState.Cancelled());

        var vm = new ReportsOverviewViewModel(state);

        Assert.False(vm.HasResults);
    }
}
