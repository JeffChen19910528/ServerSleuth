using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Tests.Fakes;
using ServerSleuth.Cli.Tests.Fixtures;

namespace ServerSleuth.Cli.Tests;

/// <summary>Cancellation — see skill.md (Phase 10A) §18. Cancelling must never leave a
/// partially-written report file (Phase 9C's exporter already guarantees this for whatever it
/// gets a chance to write; cancellation before that point means nothing is written at all).</summary>
public class ScanCommandCancellationTests
{
    [Fact]
    public async Task PreCancelledToken_ReturnsCancelledExitCode_NoStackTrace()
    {
        using var temp = new TempDirectory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));
        var (exitCode, _, stderr) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine, cts.Token);

        Assert.Equal(CliExitCode.Cancelled, exitCode);
        Assert.Contains("cancelled", stderr, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at ServerSleuth", stderr, StringComparison.Ordinal); // no stack trace
    }

    [Fact]
    public async Task PreCancelledToken_NeverWritesAReportFile()
    {
        using var temp = new TempDirectory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));
        await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine, cts.Token);

        Assert.False(File.Exists(Path.Combine(temp.Path, "report.json")));
        Assert.False(File.Exists(Path.Combine(temp.Path, "report.html")));
    }

    /// <summary>
    /// Phase 10B §9: cancellation must stop the pipeline at the "next supported cancellation
    /// boundary" even when the request arrives WHILE discovery is running, not only when the
    /// token was already cancelled before the scan started (the two prior tests). Deterministic —
    /// no real-time keyboard input, no timing race: <see cref="CancellingFakeDiscoveryEngine"/>
    /// cancels the exact <see cref="CancellationTokenSource"/> the test holds from inside its own
    /// <c>RunAsync</c>, then throws via <c>ThrowIfCancellationRequested</c>, exactly like a real
    /// scanner loop checking its token mid-run would.
    /// </summary>
    [Fact]
    public async Task CancellationDuringDiscovery_StopsBeforeAnalysis_ReturnsCancelledExitCode_NoReportWritten()
    {
        using var temp = new TempDirectory();
        using var cts = new CancellationTokenSource();
        var engine = new CancellingFakeDiscoveryEngine(cts);

        var (exitCode, _, stderr) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine, cts.Token);

        Assert.Equal(CliExitCode.Cancelled, exitCode);
        Assert.Contains("cancelled", stderr, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(temp.Path, "report.json")));
        Assert.False(File.Exists(Path.Combine(temp.Path, "report.html")));
    }
}
