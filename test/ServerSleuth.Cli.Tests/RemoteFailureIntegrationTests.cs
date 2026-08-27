using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Tests.Fakes;
using ServerSleuth.Cli.Tests.Fixtures;
using ServerSleuth.Core.Targets;

namespace ServerSleuth.Cli.Tests;

/// <summary>
/// Phase 10E-2 §10, §11, §14, §15: cancellation and failure hardening SPECIFICALLY for a remote
/// target flowing through the real CLI/pipeline — extends Phase 10E-1's
/// <see cref="RemotePipelineIntegrationTests"/> (which proved successful/partial remote scans)
/// with the cancellation-under-a-remote-target scenarios <see cref="ScanCommandCancellationTests"/>
/// already proves for local. No live remote host is needed — <see cref="FakeRemoteTargetTransport"/>
/// carries the remote target identity while discovery itself is faked, exactly like Phase 10E-1.
/// </summary>
public class RemoteFailureIntegrationTests
{
    private static readonly ScanTarget RemoteTarget = ScanTarget.Remote("remote-linux-host.internal", TargetPlatform.Linux, 22);

    private static readonly string[] SshArgs =
        ["--target", "remote-linux-host.internal", "--ssh-user", "tester", "--ssh-key", "/fake/never-read-key",
         "--ssh-host-fingerprint", "aa:bb:cc"];

    [Fact]
    public async Task RemoteTarget_PreCancelledToken_ReturnsCancelledExitCode_NoReportWritten_NoStackTrace()
    {
        using var temp = new TempDirectory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));
        var transport = new FakeRemoteTargetTransport(RemoteTarget);

        var args = new List<string> { "scan", "--output", temp.Path };
        args.AddRange(SshArgs);
        var (exitCode, _, stderr) = await CliTestRunner.RunAsync([.. args], engine, cts.Token, transport);

        Assert.Equal(CliExitCode.Cancelled, exitCode);
        Assert.Contains("cancelled", stderr, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at ServerSleuth", stderr, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(temp.Path, "report.json")));
        Assert.False(File.Exists(Path.Combine(temp.Path, "report.html")));
    }

    [Fact]
    public async Task RemoteTarget_CancellationDuringDiscovery_StopsBeforeAnalysis_NoReportWritten()
    {
        using var temp = new TempDirectory();
        using var cts = new CancellationTokenSource();
        var engine = new CancellingFakeDiscoveryEngine(cts);
        var transport = new FakeRemoteTargetTransport(RemoteTarget);

        var args = new List<string> { "scan", "--output", temp.Path };
        args.AddRange(SshArgs);
        var (exitCode, _, stderr) = await CliTestRunner.RunAsync([.. args], engine, cts.Token, transport);

        Assert.Equal(CliExitCode.Cancelled, exitCode);
        Assert.Contains("cancelled", stderr, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(temp.Path, "report.json")));
        Assert.False(File.Exists(Path.Combine(temp.Path, "report.html")));
    }

    // 9.1/9.4/9.5: an unexpected exception from a remote-target discovery run still maps to
    // GeneralFailure — never an unhandled crash, never fake success, never a local fallback
    // (nothing in this path could fall back — FakeRemoteTargetTransport's ProcessRunner/
    // FileSystemReader would throw a distinct sentinel exception if ever invoked, and this test
    // asserts the ACTUAL thrown exception's message reached the user, proving the sentinel was
    // never triggered).
    [Fact]
    public async Task RemoteTarget_UnexpectedDiscoveryException_ReturnsGeneralFailure_NeverCrashesTheProcess_NeverFabricatesSuccess()
    {
        using var temp = new TempDirectory();
        var engine = new ThrowingFakeDiscoveryEngine(new InvalidOperationException("simulated remote transport failure"));
        var transport = new FakeRemoteTargetTransport(RemoteTarget);

        var args = new List<string> { "scan", "--output", temp.Path };
        args.AddRange(SshArgs);
        var (exitCode, stdout, stderr) = await CliTestRunner.RunAsync([.. args], engine, transport: transport);

        Assert.Equal(CliExitCode.GeneralFailure, exitCode);
        Assert.Contains("simulated remote transport failure", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("Completed.", stdout, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(temp.Path, "report.json")));
    }

    // 15: determinism of a remote cancellation scenario across two independent runs.
    [Fact]
    public async Task RemoteTarget_RepeatedIdenticalCancellation_IsDeterministic()
    {
        using var tempA = new TempDirectory();
        using var tempB = new TempDirectory();
        using var ctsA = new CancellationTokenSource();
        using var ctsB = new CancellationTokenSource();

        var argsA = new List<string> { "scan", "--output", tempA.Path };
        argsA.AddRange(SshArgs);
        var argsB = new List<string> { "scan", "--output", tempB.Path };
        argsB.AddRange(SshArgs);

        var (exitCodeA, _, stderrA) = await CliTestRunner.RunAsync([.. argsA], new CancellingFakeDiscoveryEngine(ctsA), ctsA.Token, new FakeRemoteTargetTransport(RemoteTarget));
        var (exitCodeB, _, stderrB) = await CliTestRunner.RunAsync([.. argsB], new CancellingFakeDiscoveryEngine(ctsB), ctsB.Token, new FakeRemoteTargetTransport(RemoteTarget));

        Assert.Equal(exitCodeA, exitCodeB);
        Assert.Equal(CliExitCode.Cancelled, exitCodeA);
        Assert.Equal(stderrA, stderrB);
    }
}
