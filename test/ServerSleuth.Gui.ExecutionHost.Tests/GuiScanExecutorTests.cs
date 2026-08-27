using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.ExecutionHost.Tests.Fakes;
using ServerSleuth.Gui.ExecutionHost.Tests.Fixtures;
using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.ExecutionHost.Tests;

/// <summary>GUI-3 §Step13: <see cref="GuiScanExecutor"/> exercised entirely through fakes — no
/// real scanner, transport, or network/filesystem access beyond the temp report directory each
/// test creates and deletes for itself.</summary>
public sealed class GuiScanExecutorTests : IDisposable
{
    private readonly string _outputDirectory = Path.Combine(Path.GetTempPath(), "serversleuth-gui-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }

    private static ScanRequest LocalRequest(string outputDirectory) => new()
    {
        Target = ScanTarget.Local(TargetPlatform.Windows),
        OutputDirectory = outputDirectory,
        OutputFormat = ScanOutputFormat.Both,
        OverwritePolicy = ScanOverwritePolicy.Overwrite,
        Verbose = false
    };

    private static GuiScanExecutor BuildExecutor(AggregateDiscoveryResultDelayOptions? options = null)
    {
        var discoveryEngine = new FakeDiscoveryEngine(MinimalFixture.Build(), options?.Delay);
        return new GuiScanExecutor((request, _) =>
        {
            var services = new ServiceCollection();
            services.AddSingleton<ServerSleuth.Core.Orchestration.IDiscoveryEngine>(discoveryEngine);
            var provider = services.BuildServiceProvider();
            return new GuiScanComposition { Transport = new FakeTargetTransport(request.Target), Services = provider };
        });
    }

    private sealed record AggregateDiscoveryResultDelayOptions
    {
        public TimeSpan? Delay { get; init; }
    }

    // 1. Valid local execution.
    [Fact]
    public async Task ExecuteAsync_ForLocalTarget_CompletesSuccessfully()
    {
        var executor = BuildExecutor();
        var progress = new List<ScanProgressState>();

        var completion = await executor.ExecuteAsync(
            LocalRequest(_outputDirectory), ScanCredentialInput.Empty, new SynchronousProgress<ScanProgressState>(progress.Add), CancellationToken.None);

        Assert.Equal(ScanExecutionStatus.Partial, completion.Status); // MinimalFixture has one PartiallySupported scanner.
        Assert.Equal(2, completion.EntityCount);
        Assert.Contains("report.json", completion.OutputPaths);
        Assert.Contains("report.html", completion.OutputPaths);
    }

    // 6, 7. Progress is surfaced, and stage transitions are deterministic.
    [Fact]
    public async Task ExecuteAsync_ReportsStages_InTheExpectedOrder()
    {
        var executor = BuildExecutor();
        var progress = new List<ScanProgressState>();

        await executor.ExecuteAsync(LocalRequest(_outputDirectory), ScanCredentialInput.Empty, new SynchronousProgress<ScanProgressState>(progress.Add), CancellationToken.None);

        var stages = progress.Select(p => p.Stage).ToList();
        Assert.Equal(
            [ScanStage.Preparing, ScanStage.Discovery, ScanStage.Discovery, ScanStage.Analysis, ScanStage.RiskAnalysis, ScanStage.MigrationAssessment, ScanStage.Reporting, ScanStage.Export],
            stages);
    }

    // 8, 9. Scanner status/entity counts are surfaced from the real discovery result — never fabricated.
    [Fact]
    public async Task ExecuteAsync_SurfacesRealScannerStatusesAndEntityCounts()
    {
        var executor = BuildExecutor();
        var progress = new List<ScanProgressState>();

        var completion = await executor.ExecuteAsync(
            LocalRequest(_outputDirectory), ScanCredentialInput.Empty, new SynchronousProgress<ScanProgressState>(progress.Add), CancellationToken.None);

        Assert.Equal(2, completion.ScannerStatuses.Count);
        Assert.Contains(completion.ScannerStatuses, s => s.ScannerId == "services-scanner" && s.EntityCount == 1);
        Assert.Contains(completion.ScannerStatuses, s => s.ScannerId == "registry-scanner" && s.EntityCount == 1);
    }

    // 10, 27. Cancellation propagates and never leaves a partial export treated as success.
    [Fact]
    public async Task ExecuteAsync_WhenCancelledDuringDiscovery_ReturnsCancelled_AndWritesNoReport()
    {
        var executor = BuildExecutor(new AggregateDiscoveryResultDelayOptions { Delay = TimeSpan.FromSeconds(30) });
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var completion = await executor.ExecuteAsync(
            LocalRequest(_outputDirectory), ScanCredentialInput.Empty, new SynchronousProgress<ScanProgressState>(_ => { }), cts.Token);

        Assert.Equal(ScanExecutionStatus.Cancelled, completion.Status);
        Assert.False(Directory.Exists(_outputDirectory) && Directory.EnumerateFiles(_outputDirectory).Any());
    }

    // 15. A composition failure produces a Failed state, never an unhandled exception.
    [Fact]
    public async Task ExecuteAsync_WhenCompositionThrows_ReturnsFailed_WithAGenericMessage()
    {
        var executor = new GuiScanExecutor((_, _) => throw new InvalidOperationException("boom"));

        var completion = await executor.ExecuteAsync(
            LocalRequest(_outputDirectory), ScanCredentialInput.Empty, new SynchronousProgress<ScanProgressState>(_ => { }), CancellationToken.None);

        Assert.Equal(ScanExecutionStatus.Failed, completion.Status);
        Assert.NotNull(completion.ErrorMessage);
        Assert.DoesNotContain("boom", completion.ErrorMessage);
    }

    // 19. Credentials never enter logs/error messages — sentinel sweep of the generic failure message.
    [Fact]
    public async Task ExecuteAsync_WhenCompositionThrowsWithASensitiveMessage_NeverLeaksItToTheCaller()
    {
        const string sentinel = "SERVER_SLEUTH_TEST_SENTINEL_PASSWORD_x7q";
        var executor = new GuiScanExecutor((_, _) => throw new InvalidOperationException($"Connection failed for user with password {sentinel}"));

        var completion = await executor.ExecuteAsync(
            LocalRequest(_outputDirectory), ScanCredentialInput.Empty, new SynchronousProgress<ScanProgressState>(_ => { }), CancellationToken.None);

        Assert.DoesNotContain(sentinel, completion.ErrorMessage);
    }

    // GUI-4 §Step2: the real ScanPipelineResult (Aggregation + Report) must reach ScanCompletionState
    // unchanged — the single source of truth the Results Dashboard is built from.
    [Fact]
    public async Task ExecuteAsync_OnSuccess_CarriesTheRealPipelineResult()
    {
        var executor = BuildExecutor();

        var completion = await executor.ExecuteAsync(
            LocalRequest(_outputDirectory), ScanCredentialInput.Empty, new SynchronousProgress<ScanProgressState>(_ => { }), CancellationToken.None);

        Assert.NotNull(completion.PipelineResult);
        Assert.NotNull(completion.PipelineResult!.Report);
        Assert.NotNull(completion.PipelineResult.Aggregation);
    }

    // GUI-4 §Step3: a cancelled scan never fabricates a pipeline result.
    [Fact]
    public async Task ExecuteAsync_WhenCancelled_NeverCarriesAPipelineResult()
    {
        var executor = BuildExecutor(new AggregateDiscoveryResultDelayOptions { Delay = TimeSpan.FromSeconds(30) });
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var completion = await executor.ExecuteAsync(
            LocalRequest(_outputDirectory), ScanCredentialInput.Empty, new SynchronousProgress<ScanProgressState>(_ => { }), cts.Token);

        Assert.Null(completion.PipelineResult);
    }

    // GUI-6 §3: a remote target whose transport/connect step fails must surface the exact same
    // generic Failed(...) shape as any other composition failure — never an unhandled exception,
    // never a raw connect/transport error message, and never a fabricated PipelineResult.
    [Fact]
    public async Task ExecuteAsync_ForARemoteTarget_WhenTheTransportFailsToConnect_ReturnsFailed_WithAGenericMessage_AndNoPipelineResult()
    {
        var remoteTarget = ScanTarget.Remote("unreachable-host.example", TargetPlatform.Linux);
        var executor = new GuiScanExecutor((_, _) =>
            throw new InvalidOperationException("SSH connect to unreachable-host.example failed: Connection refused"));
        var request = LocalRequest(_outputDirectory) with { Target = remoteTarget };

        var completion = await executor.ExecuteAsync(
            request, ScanCredentialInput.Empty, new SynchronousProgress<ScanProgressState>(_ => { }), CancellationToken.None);

        Assert.Equal(ScanExecutionStatus.Failed, completion.Status);
        Assert.NotNull(completion.ErrorMessage);
        Assert.DoesNotContain("Connection refused", completion.ErrorMessage);
        Assert.DoesNotContain("unreachable-host.example", completion.ErrorMessage);
        Assert.Null(completion.PipelineResult);
    }

    // 28. Repeated execution with the same fixture produces an equivalent result.
    [Fact]
    public async Task ExecuteAsync_CalledTwiceWithTheSameFixture_ProducesEquivalentEntityAndOutputCounts()
    {
        var executor1 = BuildExecutor();
        var executor2 = BuildExecutor();

        var first = await executor1.ExecuteAsync(LocalRequest(_outputDirectory + "-1"), ScanCredentialInput.Empty, new SynchronousProgress<ScanProgressState>(_ => { }), CancellationToken.None);
        var second = await executor2.ExecuteAsync(LocalRequest(_outputDirectory + "-2"), ScanCredentialInput.Empty, new SynchronousProgress<ScanProgressState>(_ => { }), CancellationToken.None);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.EntityCount, second.EntityCount);
        Assert.Equal(first.OutputPaths.Count, second.OutputPaths.Count);

        Directory.Delete(_outputDirectory + "-1", recursive: true);
        Directory.Delete(_outputDirectory + "-2", recursive: true);
    }
}
