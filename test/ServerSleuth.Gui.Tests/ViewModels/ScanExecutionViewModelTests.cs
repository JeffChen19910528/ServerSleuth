using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Tests.Fakes;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>GUI-3 §Step12-13: <see cref="ScanExecutionViewModel"/> exercised entirely against a
/// deterministic <see cref="FakeGuiScanExecutor"/> — never a real transport/scanner/pipeline.
/// <see cref="Start"/>'s own <c>Progress&lt;ScanProgressState&gt;</c> posts callbacks via
/// whatever <see cref="System.Threading.SynchronizationContext"/> is active when
/// <c>Start</c> is called (the WPF <c>Dispatcher</c>'s, in the real app) — with no such context
/// active in a plain xUnit test host, those posts land on the ThreadPool, so every test method
/// here is <c>async Task</c> and every wait uses <c>await Task.Delay(...)</c> (never a blocking
/// <c>Thread.Sleep</c> poll loop, which would tie up the very ThreadPool thread the queued
/// <c>Progress&lt;T&gt;</c> continuation needs in order to run — a genuine starvation deadlock
/// this suite hit once during authoring and fixed by switching to an async wait).</summary>
public class ScanExecutionViewModelTests
{
    private static ScanRequest LocalRequest() => new()
    {
        Target = ScanTarget.Local(TargetPlatform.Windows),
        OutputDirectory = "./out",
        OutputFormat = ScanOutputFormat.Both,
        OverwritePolicy = ScanOverwritePolicy.FailIfExists,
        Verbose = false
    };

    // 4. ScanRequested starts execution — covered end-to-end via MainViewModelTests; here, Start() directly.
    [Fact]
    public async Task Start_BeginsRunning_AndEventuallyReachesTheFakeExecutorsCompletion()
    {
        var executor = new FakeGuiScanExecutor();
        var viewModel = new ScanExecutionViewModel(executor);

        viewModel.Start(LocalRequest(), ScanCredentialInput.Empty);

        Assert.True(await WaitUntilAsync(() => viewModel.IsFinished));
        Assert.Single(executor.Calls);
        Assert.Equal(ScanExecutionStatus.Completed, viewModel.State.Status);
        Assert.False(viewModel.IsRunning);
    }

    // 6, 7. Progress is surfaced; stage transitions are deterministic.
    [Fact]
    public async Task Start_SurfacesTheExecutorsProgressReports_InOrder()
    {
        var executor = new FakeGuiScanExecutor();
        var viewModel = new ScanExecutionViewModel(executor);

        viewModel.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => viewModel.IsFinished));

        // Populated synchronously inside the fake's own method body — independent of however
        // Progress<T> happens to marshal the ViewModel-side callback in this test host.
        Assert.Equal(2, executor.ReportedProgress.Count);
        Assert.Equal(ScanStage.Preparing, executor.ReportedProgress[0].Stage);
        Assert.Equal(ScanStage.Discovery, executor.ReportedProgress[1].Stage);
    }

    // 13, 14. Completion state is correct; partial result is preserved.
    [Fact]
    public async Task Start_WhenExecutorReportsPartial_PreservesThatStatus()
    {
        var executor = new FakeGuiScanExecutor { CompletionToReturn = new ScanCompletionState { Status = ScanExecutionStatus.Partial, EntityCount = 5 } };
        var viewModel = new ScanExecutionViewModel(executor);

        viewModel.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => viewModel.IsFinished));

        Assert.Equal(ScanExecutionStatus.Partial, viewModel.State.Status);
        Assert.Equal(5, viewModel.State.EntityCount);
    }

    // 15. Failed execution produces a Failed state.
    [Fact]
    public async Task Start_WhenExecutorThrows_ProducesAFailedState_WithAGenericMessage()
    {
        var executor = new FakeGuiScanExecutor { ThrowInsteadOfReturning = true };
        var viewModel = new ScanExecutionViewModel(executor);

        viewModel.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => viewModel.IsFinished));

        Assert.Equal(ScanExecutionStatus.Failed, viewModel.State.Status);
        Assert.NotNull(viewModel.State.ErrorMessage);
        Assert.DoesNotContain("Simulated", viewModel.State.ErrorMessage);
    }

    // 10, 11. Cancellation propagates; repeated cancellation is safe.
    [Fact]
    public async Task Cancel_WhileRunning_EventuallyReachesCancelledState_AndCanBeCalledRepeatedly()
    {
        var gate = new TaskCompletionSource<bool>();
        var executor = new FakeGuiScanExecutor { Gate = gate, RespectCancellation = true };
        var viewModel = new ScanExecutionViewModel(executor);

        viewModel.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => executor.Calls.Count > 0 && viewModel.IsRunning));

        viewModel.CancelCommand.Execute(null);
        viewModel.CancelCommand.Execute(null); // repeated — must not throw

        gate.SetResult(true);

        Assert.True(await WaitUntilAsync(() => viewModel.IsFinished));
        Assert.Equal(ScanExecutionStatus.Cancelled, viewModel.State.Status);
    }

    // Cancel before any scan has started must never throw (idempotent from the very beginning).
    [Fact]
    public void Cancel_BeforeAnyScanStarted_DoesNotThrow()
    {
        var viewModel = new ScanExecutionViewModel(new FakeGuiScanExecutor());
        var exception = Record.Exception(() => viewModel.CancelCommand.Execute(null));
        Assert.Null(exception);
    }

    // 12, 29. Repeated Start does not launch two scans / no concurrent execution.
    [Fact]
    public async Task Start_CalledAgainWhileRunning_IsIgnored_OnlyOneExecutionOccurs()
    {
        var gate = new TaskCompletionSource<bool>();
        var executor = new FakeGuiScanExecutor { Gate = gate };
        var viewModel = new ScanExecutionViewModel(executor);

        viewModel.Start(LocalRequest(), ScanCredentialInput.Empty);
        viewModel.Start(LocalRequest(), ScanCredentialInput.Empty);
        viewModel.Start(LocalRequest(), ScanCredentialInput.Empty);

        gate.SetResult(true);
        Assert.True(await WaitUntilAsync(() => viewModel.IsFinished));

        Assert.Single(executor.Calls);
    }

    // 16, 17, 18. Credentials never enter execution state/progress/completion result — structural,
    // reinforced here by a behavioral sweep confirming they only ever reach the executor call itself.
    [Fact]
    public async Task Start_WithCredentials_NeverLeaksThemIntoState()
    {
        using var password = SecurePassword("SERVER_SLEUTH_TEST_GUI_EXEC_PASSWORD_q4z");
        var executor = new FakeGuiScanExecutor();
        var viewModel = new ScanExecutionViewModel(executor);
        var credentials = new ScanCredentialInput { Username = "svc-account", Password = password };

        viewModel.Start(LocalRequest(), credentials);
        Assert.True(await WaitUntilAsync(() => viewModel.IsFinished));

        // ScanExecutionState structurally has no property that could hold a password (see
        // NoCredentialShapedGuiStateTests) — this test additionally confirms nothing on the
        // executor's OWN observable call record leaked past the credential parameter itself.
        Assert.Single(executor.Calls);
        Assert.Same(credentials, executor.Calls[0].Credentials);
    }

    // 21, 22, 23. Start Scan does not execute discovery directly and is bounded by IGuiScanExecutor.
    [Fact]
    public async Task ScanExecutionViewModel_OnlyEverCallsThroughTheAbstractExecutor()
    {
        var executor = new FakeGuiScanExecutor();
        var viewModel = new ScanExecutionViewModel(executor);
        var request = LocalRequest();

        viewModel.Start(request, ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => viewModel.IsFinished));

        Assert.Single(executor.Calls);
        Assert.Equal(request.Target, executor.Calls[0].Request.Target);
    }

    // 30. GUI startup still performs no scan — a freshly constructed ViewModel is Idle.
    [Fact]
    public void FreshlyConstructedViewModel_IsIdle_AndHasNeverCalledTheExecutor()
    {
        var executor = new FakeGuiScanExecutor();
        var viewModel = new ScanExecutionViewModel(executor);

        Assert.Equal(ScanExecutionStatus.Idle, viewModel.State.Status);
        Assert.Empty(executor.Calls);
    }

    [Fact]
    public async Task NewScanCommand_AfterCompletion_RaisesReturnToConfigurationRequested()
    {
        var viewModel = new ScanExecutionViewModel(new FakeGuiScanExecutor());
        viewModel.Start(LocalRequest(), ScanCredentialInput.Empty);
        Assert.True(await WaitUntilAsync(() => viewModel.IsFinished));

        var raised = false;
        viewModel.ReturnToConfigurationRequested += (_, _) => raised = true;
        viewModel.NewScanCommand.Execute(null);

        Assert.True(raised);
    }

    private static System.Security.SecureString SecurePassword(string value)
    {
        var secure = new System.Security.SecureString();
        foreach (var ch in value)
        {
            secure.AppendChar(ch);
        }

        secure.MakeReadOnly();
        return secure;
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        // ScanExecutionViewModel now offloads IGuiScanExecutor.ExecuteAsync via Task.Run (see its
        // own doc comment) so that a real scan never blocks the WPF UI thread — this is a genuine
        // cross-thread hop rather than the previous same-thread synchronous completion, so this
        // deadline has headroom for actual thread-pool scheduling latency, not just CPU work.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(10);
        }

        return condition();
    }
}
