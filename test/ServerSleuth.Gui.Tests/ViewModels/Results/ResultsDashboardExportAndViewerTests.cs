using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Tests.Fakes;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.ViewModels.Results;

/// <summary>
/// GUI-5 §1-4, §12: the Results Dashboard's "Export Report"/"Open Report"/"New Scan" actions —
/// exercised entirely through <see cref="FakeGuiReportExportService"/>/<see cref="FakeGuiReportViewerService"/>,
/// never a real file or the real Reporting APIs (that boundary is <see cref="ServerSleuth.Gui.ExecutionHost.GuiReportExportService"/>'s
/// own responsibility, tested separately in ServerSleuth.Gui.ExecutionHost.Tests).
/// </summary>
public class ResultsDashboardExportAndViewerTests
{
    private static ResultsDashboardViewModel BuildDashboard(
        FakeGuiReportExportService? exportService = null, FakeGuiReportViewerService? viewerService = null,
        ScanResultFixtureFactory.Options? options = null)
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(options ?? new ScanResultFixtureFactory.Options { ApplicationCount = 1 })
            with
        { OutputDirectory = @"C:\fixture-output" };
        return new ResultsDashboardViewModel(state, exportService, viewerService);
    }

    // ----- EXPORT -----

    [Fact]
    public void ExportDirectory_DefaultsToTheScansOwnOutputDirectory()
    {
        var vm = BuildDashboard();
        Assert.Equal(@"C:\fixture-output", vm.ExportDirectory);
    }

    [Fact]
    public void ExportReportCommand_CanExecute_IsFalse_WithNoExportServiceSupplied()
    {
        var vm = BuildDashboard();
        Assert.False(vm.ExportReportCommand.CanExecute(null));
    }

    [Fact]
    public void ExportReportCommand_CanExecute_IsFalse_WhenThereIsNoReport()
    {
        var state = ScanExecutionState.StartingFor(ScanTarget.Local()).WithCompletion(ScanCompletionState.Cancelled());
        var vm = new ResultsDashboardViewModel(state, new FakeGuiReportExportService(), new FakeGuiReportViewerService());

        Assert.False(vm.ExportReportCommand.CanExecute(null));
    }

    [Fact]
    public void ExportReportCommand_CanExecute_IsTrue_OnceAnExportServiceAndDirectoryArePresent()
    {
        var vm = BuildDashboard(new FakeGuiReportExportService());
        Assert.True(vm.ExportReportCommand.CanExecute(null));
    }

    [Fact]
    public void ExportReportCommand_CanExecute_IsFalse_WhenDirectoryIsBlank()
    {
        var vm = BuildDashboard(new FakeGuiReportExportService());
        vm.ExportDirectory = "   ";
        Assert.False(vm.ExportReportCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(ScanOutputFormat.Json)]
    [InlineData(ScanOutputFormat.Html)]
    [InlineData(ScanOutputFormat.Both)]
    public void ExportReportCommand_InvokesTheExportService_WithTheChosenFormat(ScanOutputFormat format)
    {
        var export = new FakeGuiReportExportService();
        var vm = BuildDashboard(export);
        vm.ExportFormat = format;

        vm.ExportReportCommand.Execute(null);

        var call = Assert.Single(export.Calls);
        Assert.Equal(format, call.Format);
        Assert.Equal(vm.Report, call.Report); // the EXACT same report instance — never a copy.
        Assert.Equal(@"C:\fixture-output", call.OutputDirectory);
    }

    [Theory]
    [InlineData(ScanOverwritePolicy.FailIfExists)]
    [InlineData(ScanOverwritePolicy.Overwrite)]
    public void ExportReportCommand_InvokesTheExportService_WithTheChosenOverwritePolicy(ScanOverwritePolicy policy)
    {
        var export = new FakeGuiReportExportService();
        var vm = BuildDashboard(export);
        vm.ExportOverwritePolicy = policy;

        vm.ExportReportCommand.Execute(null);

        Assert.Equal(policy, Assert.Single(export.Calls).OverwritePolicy);
    }

    [Fact]
    public void ExportReportCommand_OnSuccess_PublishesTheExactResultTheServiceReturned_NeverFabricated()
    {
        var export = new FakeGuiReportExportService { ResultToReturn = GuiReportExportResult.Succeeded(["report.json"]) };
        var vm = BuildDashboard(export);

        vm.ExportReportCommand.Execute(null);

        Assert.NotNull(vm.LastExportResult);
        Assert.True(vm.LastExportResult!.Success);
        Assert.Equal(["report.json"], vm.LastExportResult.WrittenFileNames);
        Assert.Equal("report.json", vm.LastExportedFileNamesText);
    }

    [Fact]
    public void ExportReportCommand_OnFailIfExistsCollision_PublishesTheFailureReason_NeverFabricatingSuccess()
    {
        var export = new FakeGuiReportExportService
        {
            ResultToReturn = GuiReportExportResult.Failed(GuiReportExportFailureReason.AlreadyExists, "One or more report files already exist.")
        };
        var vm = BuildDashboard(export);

        vm.ExportReportCommand.Execute(null);

        Assert.False(vm.LastExportResult!.Success);
        Assert.Equal(GuiReportExportFailureReason.AlreadyExists, vm.LastExportResult.FailureReason);
        Assert.DoesNotContain("Exception", vm.LastExportResult.ErrorMessage);
    }

    [Fact]
    public void ExportReportCommand_ExecutedTwice_WithJsonThenHtml_CallsTheServiceExactlyTwice_NeverRunsAScan()
    {
        var export = new FakeGuiReportExportService();
        var vm = BuildDashboard(export);

        vm.ExportFormat = ScanOutputFormat.Json;
        vm.ExportReportCommand.Execute(null);
        vm.ExportFormat = ScanOutputFormat.Html;
        vm.ExportReportCommand.Execute(null);

        Assert.Equal(2, export.Calls.Count);
        Assert.Equal(ScanOutputFormat.Json, export.Calls[0].Format);
        Assert.Equal(ScanOutputFormat.Html, export.Calls[1].Format);
    }

    [Fact]
    public void ExportReportCommand_NeverMutatesTheReport()
    {
        var export = new FakeGuiReportExportService();
        var vm = BuildDashboard(export);
        var reportBefore = vm.Report;
        var applicationCountBefore = vm.Report!.ApplicationAssessments.Count;

        vm.ExportReportCommand.Execute(null);

        Assert.Same(reportBefore, vm.Report);
        Assert.Equal(applicationCountBefore, vm.Report!.ApplicationAssessments.Count);
    }

    // ----- REPORT VIEWER -----

    [Fact]
    public void OpenReportCommand_CanExecute_IsFalse_WithNoViewerServiceSupplied()
    {
        var vm = BuildDashboard();
        Assert.False(vm.OpenReportCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedReportFileName_DefaultsToTheFirstReportFile()
    {
        var vm = BuildDashboard(viewerService: new FakeGuiReportViewerService());
        Assert.Equal(vm.ReportFileNames[0], vm.SelectedReportFileName);
    }

    [Fact]
    public void OpenReportCommand_ReadsTheSelectedFile_FromTheScansOwnOutputDirectory_NeverRegeneratingIt()
    {
        var viewer = new FakeGuiReportViewerService { ResultToReturn = GuiReportViewResult.Succeeded("{\"schemaVersion\":1}") };
        var vm = BuildDashboard(viewerService: viewer);
        vm.SelectedReportFileName = "report.json";

        vm.OpenReportCommand.Execute(null);

        var call = Assert.Single(viewer.Calls);
        Assert.Equal(@"C:\fixture-output", call.OutputDirectory);
        Assert.Equal("report.json", call.FileName);
        Assert.True(vm.ReportViewResult!.Success);
        Assert.Equal("{\"schemaVersion\":1}", vm.ReportViewResult.Content);
    }

    [Fact]
    public void OpenReportCommand_OnFailure_NeverFabricatesContent()
    {
        var viewer = new FakeGuiReportViewerService
        {
            ResultToReturn = GuiReportViewResult.Failed(GuiReportViewFailureReason.NotFound, "The requested report file could not be located.")
        };
        var vm = BuildDashboard(viewerService: viewer);

        vm.OpenReportCommand.Execute(null);

        Assert.False(vm.ReportViewResult!.Success);
        Assert.Null(vm.ReportViewResult.Content);
    }

    // ----- NEW SCAN -----

    [Fact]
    public void NewScanCommand_RaisesNewScanRequested_ExactlyOnce()
    {
        var vm = BuildDashboard();
        var raiseCount = 0;
        vm.NewScanRequested += (_, _) => raiseCount++;

        vm.NewScanCommand.Execute(null);

        Assert.Equal(1, raiseCount);
    }
}
